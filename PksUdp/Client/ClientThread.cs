using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PksUdp.Client
{
    internal class ClientThread
    {
        private readonly Timer _pingTimer = new Timer {Interval = 30000, AutoReset = true};

        /// <summary>
        ///     Udp Socket.
        /// </summary>
        private readonly PksClient _pksClient;

        /// <summary>
        ///     Timer pre znovu vyziadanie fragmentov.
        /// </summary>
        private readonly Timer _recieveTimer = new Timer {Interval = 10000, AutoReset = false};

        private bool _connected;
        private PaketFragments _lastMessage;

        private bool _secondTry;

        public ClientThread(PksClient pksClient)
        {
            _pksClient = pksClient;
            _recieveTimer.Elapsed += _recieveTimer_Elapsed;
            _pingTimer.Elapsed += _pingTimer_Elapsed;
        }

        private void _pingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Ping();
        }

        private void Ping()
        {
            var data = Extensions.PingPaket();
            _pksClient.Socket?.Client?.Send(data, data.Length, SocketFlags.None);
        }

        private void _recieveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_lastMessage == null) return;
            if (_lastMessage is FileFragments)
                _pksClient.OnReceivedFile(_lastMessage.PaketId, false);
            else
                _pksClient.OnReceivedMessage(_lastMessage.PaketId, false);
            _lastMessage = null;
        }


        internal void RecieveLoop()
        {
            try
            {
                _pksClient.Socket.Connect(_pksClient.EndPoint);
                var data = Extensions.ConnectedPaket();
                _pksClient.Socket.Client.Send(data, data.Length, SocketFlags.None);
                _pingTimer.Start();

                // Here will be saved information about UDP sender.
                for (;;)
                {
                    _recieveTimer.Start();

                    var task = _pksClient.Socket.ReceiveAsync();
                    for (;;)
                    {
                        if (task.IsFaulted)
                        {
                            if (task.Exception != null) throw task.Exception;
                            break;
                        }
                        if (task.IsCompleted)
                        {
                            _recieveTimer.Stop();

                            if (task.IsFaulted)
                            {
                                if (task.Exception != null) throw task.Exception;
                                break;
                            }

                            var bytes = task.Result.Buffer;
                            var rcv = task.Result.RemoteEndPoint;

                            if (_connected)
                                if (!rcv.Equals(_pksClient.EndPoint))
                                    break;

                            DecodePaket(bytes);
                            break;
                        }

                        if (_lastMessage == null)
                            SendFragments();
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (SocketException ex)
            {
                _pksClient.OnSocketException(ex);
            }
            catch (Exception ex)
            {
                var exception = ex.InnerException as SocketException;
                if (exception != null)
                    _pksClient.OnSocketException(exception);
                else
                    _pksClient.OnClientError();
            }
            finally
            {
                _pingTimer.Stop();
                _recieveTimer.Stop();
                _connected = false;
                _lastMessage?.Fragments.Clear();
            }
        }

        private void DecodePaket(byte[] bytes)
        {
            if (CheckFragment(bytes)) return;

            Extensions.Type type;
            try
            {
                type = bytes.GetPaketType();
            }
            catch (Exception)
            {
                return;
            }

            if (NoConnection(type)) return;

            if (type == Extensions.Type.Ping) return;

            if (_lastMessage == null)
                return;

            switch (type)
            {
                case Extensions.Type.RetryFragment:
                    ResendFragments(bytes);
                    return;
                case Extensions.Type.SuccessFull:
                    if (ZleId(bytes)) return;
                    if (_lastMessage is FileFragments)
                        _pksClient.OnReceivedFile(_lastMessage.PaketId, true);
                    else
                        _pksClient.OnReceivedMessage(_lastMessage.PaketId, true);
                    _lastMessage = null;
                    return;
                case Extensions.Type.Fail:
                    if (ZleId(bytes)) return;
                    if (_lastMessage is FileFragments)
                        _pksClient.OnReceivedFile(_lastMessage.PaketId, false);
                    else
                        _pksClient.OnReceivedMessage(_lastMessage.PaketId, false);
                    _lastMessage = null;
                    return;
                default:
                    return;
            }
        }

        private void SendFragments()
        {
            PksClient.NaOdoslanie odoslat;
            lock (_pksClient.PoradovnikLock)
            {
                if (!_pksClient.Poradovnik.Any())
                    return;
                odoslat = _pksClient.Poradovnik.Dequeue();
            }

            var odoslanie = odoslat as PksClient.SpravaNaOdoslanie;
            if (odoslanie != null)
            {
                _recieveTimer.Stop();
                _lastMessage = new MessageFragments(new PaketId());
                RozdelSpravuNaFragmenty((MessageFragments) _lastMessage, odoslanie);
                var prvy = true;
                foreach (var fragment in _lastMessage.Fragments)
                {
                    if ((odoslanie.Error == PksClient.Fragmenty.VsetkyChybne) ||
                        ((odoslanie.Error == PksClient.Fragmenty.PrvyChybny) && prvy))
                    {
                        var temp = (byte[]) fragment.Clone();
                        temp[temp.Length - 2]++;
                        _pksClient.Socket.Client.Send(temp, temp.Length, SocketFlags.None);
                        prvy = false;
                        continue;
                    }
                    _pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
                }
            }
            else
            {
                var naOdoslanie = odoslat as PksClient.SuborNaOdoslanie;
                if (naOdoslanie != null)
                {
                    var info = new FileInfo(naOdoslanie.Path);
                    if (!info.Exists)
                        return;
                    var name = Path.GetFileName(info.FullName);
                    var fileName = Encoding.UTF8.GetBytes(name);
                    var size = info.Length + fileName.Length + 4;
                    var fSize = (uint) naOdoslanie.FragmentSize;
                    if (size + 10 <= fSize)
                        SendFile(info, size, fileName);
                    else
                        SendFileFragmented(info, fSize, size, fileName);

                    _recieveTimer.Stop();
                    var prvy = true;
                    foreach (var fragment in _lastMessage.Fragments)
                    {
                        if ((naOdoslanie.Error == PksClient.Fragmenty.VsetkyChybne) ||
                            ((naOdoslanie.Error == PksClient.Fragmenty.PrvyChybny) && prvy))
                        {
                            var temp = (byte[]) fragment.Clone();
                            temp[temp.Length - 2]++;
                            _pksClient.Socket.Client.Send(temp, temp.Length, SocketFlags.None);
                            prvy = false;
                            continue;
                        }
                        _pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
                    }
                }
            }
            _recieveTimer.Start();
        }

        private void SendFileFragmented(FileInfo info, uint fSize, long size, byte[] fileName)
        {
            var id = new PaketId();
            var file = File.OpenRead(info.FullName); //File.ReadAllBytes(info.FullName);
            var fragment = new byte[fSize];
            uint order = 0;

            fragment[0] = 0x7E;
            fragment[fragment.Length - 1] = 0x7E;
            fragment.SetFragmentId(id);
            fragment.SetPaketType(Extensions.Type.File);
            fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
            fragment.SetFragmentOrder(order++);


            for (var i = 0; i < fragment.Length - 18; i++)
                if (i < 4)
                    fragment[Extensions.FragmentDataf0Index + i] = (byte) (fileName.Length >> ((3 - i)*8));
                else if (i < fileName.Length + 4)
                    fragment[Extensions.FragmentDataf0Index + i] = fileName[i - 4];
                else
                    fragment[Extensions.FragmentDataf0Index + i] = (byte) file.ReadByte();

            var zostava = size - (fSize - 18);
            var velkost = fSize - 14;
            var count = (uint) ((zostava + velkost - 1)/velkost);

            _lastMessage = new FileFragments(id);

            fragment.SetFragmentCount(count);
            fragment.CreateChecksum();

            _lastMessage.Fragments.Add(fragment);
            //_pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);

            long offset = fragment.Length - 18;

            while (size - offset > velkost)
            {
                fragment = new byte[fSize];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(id);
                fragment.SetPaketType(Extensions.Type.File);
                fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
                fragment.SetFragmentOrder(order++);
                for (var i = 0; i < fragment.Length - 14; i++)
                    if (i + offset < 4)
                        fragment[Extensions.FragmentDatafIndex + i] =
                            (byte) (fileName.Length << ((int) (3 - (i + offset))*8));
                    else if (i + offset < fileName.Length + 4)
                        fragment[Extensions.FragmentDatafIndex + i] = fileName[i + offset - 4];
                    else
                        fragment[Extensions.FragmentDatafIndex + i] = (byte) file.ReadByte();
                offset += fragment.Length - 14;
                fragment.CreateChecksum();

                _lastMessage.Fragments.Add(fragment);
                //_pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
            }


            fragment = new byte[14 + (size - offset)];
            fragment[0] = 0x7E;
            fragment[fragment.Length - 1] = 0x7E;
            fragment.SetFragmentId(id);
            fragment.SetPaketType(Extensions.Type.File);
            fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
            fragment.SetFragmentOrder(order);
            for (var i = 0; i < fragment.Length - 14; i++)
                if (i + offset < fileName.Length + 4)
                    fragment[Extensions.FragmentDatafIndex + i] = fileName[i + offset - 4];
                else
                    fragment[Extensions.FragmentDatafIndex + i] = (byte) file.ReadByte();
            fragment.CreateChecksum();
            _lastMessage.Fragments.Add(fragment);
            //_pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
        }

        private void SendFile(FileSystemInfo info, long size, IReadOnlyList<byte> fileName)
        {
            _lastMessage = new FileFragments(new PaketId());

            var file = File.ReadAllBytes(info.FullName);

            var fragment = new byte[size + 10];
            fragment[0] = 0x7E;
            fragment[fragment.Length - 1] = 0x7E;
            fragment.SetFragmentId(_lastMessage.PaketId);
            fragment.SetPaketType(Extensions.Type.File);

            fragment[Extensions.FragmentDataIndex] = (byte) (fileName.Count >> 24);
            fragment[Extensions.FragmentDataIndex + 1] = (byte) (fileName.Count >> 16);
            fragment[Extensions.FragmentDataIndex + 2] = (byte) (fileName.Count >> 8);
            fragment[Extensions.FragmentDataIndex + 3] = (byte) fileName.Count;

            for (var i = 0; i < fileName.Count; i++)
                fragment[Extensions.FragmentDataIndex + i + 4] = fileName[i];

            for (var i = 0; i < file.Length; i++)
                fragment[Extensions.FragmentDataIndex + i + 4 + fileName.Count] = file[i];

            fragment.CreateChecksum();

            _lastMessage.Fragments.Add(fragment);
            //_pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
        }

        private static void RozdelSpravuNaFragmenty(MessageFragments pksClientLastMessage,
            PksClient.SpravaNaOdoslanie sprava)
        {
            var utf8 = Encoding.UTF8.GetBytes(sprava.Sprava);
            if (sprava.FragmentSize >= utf8.Length + 10)
            {
                var fragment = new byte[utf8.Length + 10];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                //Encoding.UTF8.GetBytes(sprava.Sprava, 0, sprava.Sprava.Length, fragment, Extensions.FragmentDataIndex);
                for (var i = 0; i < utf8.Length; i++)
                    fragment[Extensions.FragmentDataIndex + i] = utf8[i];
                fragment.CreateChecksum();

                pksClientLastMessage.Fragments.Add(fragment);
            }
            else
            {
                uint order = 0;

                var fragment = new byte[sprava.FragmentSize];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
                fragment.SetFragmentOrder(order++);
                for (var i = 0; i < fragment.Length - 18; i++)
                    fragment[Extensions.FragmentDataf0Index + i] = utf8[i];
                //Encoding.UTF8.GetBytes(sprava.Sprava, 0, fragment.Length - 18, fragment, Extensions.FragmentDataf0Index);
                var zostava = utf8.Length - (fragment.Length - 18);
                var velkost = sprava.FragmentSize - 14;
                var count = (uint) ((zostava + velkost - 1)/velkost);
                fragment.SetFragmentCount(count);
                fragment.CreateChecksum();

                pksClientLastMessage.Fragments.Add(fragment);

                var offset = fragment.Length - 18;
                while (utf8.Length - offset > velkost)
                {
                    fragment = new byte[sprava.FragmentSize];
                    fragment[0] = 0x7E;
                    fragment[fragment.Length - 1] = 0x7E;
                    fragment.SetFragmentId(pksClientLastMessage.PaketId);
                    fragment.SetPaketType(Extensions.Type.Message);
                    fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
                    fragment.SetFragmentOrder(order++);
                    //Encoding.UTF8.GetBytes(sprava.Sprava, offset, fragment.Length - 14, fragment, Extensions.FragmentDatafIndex);
                    for (var i = 0; i < fragment.Length - 14; i++)
                        fragment[Extensions.FragmentDatafIndex + i] = utf8[offset + i];
                    offset += velkost;
                    fragment.CreateChecksum();
                    pksClientLastMessage.Fragments.Add(fragment);
                }

                fragment = new byte[14 + (utf8.Length - offset)];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
                fragment.SetFragmentOrder(order);
                //Encoding.UTF8.GetBytes(sprava.Sprava, offset, fragment.Length - 14, fragment, Extensions.FragmentDatafIndex);
                for (var i = 0; i < fragment.Length - 14; i++)
                    fragment[Extensions.FragmentDatafIndex + i] = utf8[offset + i];
                fragment.CreateChecksum();

                pksClientLastMessage.Fragments.Add(fragment);
            }
        }


        private bool ZleId(byte[] bytes)
        {
            var id = bytes.GetFragmentId();
            return !id.Equals(_lastMessage.PaketId);
        }

        private static bool CheckFragment(byte[] bytes)
        {
            return WrongProtocol(bytes) || !bytes.CheckChecksum();
        }

        private void ResendFragments(byte[] bytes)
        {
            if (bytes.Length < 14)
                return;


            var order = (int) bytes.GetFragmentOrder();

            if (_lastMessage.FragmentCount <= order)
                return;

            _pksClient.Socket.Client.Send(_lastMessage.Fragments[order]);
        }

        private static bool WrongProtocol(IReadOnlyList<byte> bytes)
        {
            if (bytes.Count < 5)
                return true;

            return (bytes[0] != 0x7E) && (bytes[bytes.Count - 1] != 0x7E);
        }

        private bool NoConnection(Extensions.Type type)
        {
            if (_connected) return false;

            if (type != Extensions.Type.Connect)
            {
                if (_secondTry)
                    _pksClient.OnNoServerResponse();
                _secondTry = true;
                return true;
            }

            _pksClient.OnClientConnected();
            _connected = true;
            return false;
        }
    }
}