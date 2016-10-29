using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PksUdp.Client
{
    internal class ClientThread
    {
        /// <summary>
        /// Udp Socket.
        /// </summary>
        private readonly PksClient _pksClient;

        private bool _connected;
        private PaketFragments _lastMessage;

        /// <summary>
        /// Timer pre znovu vyziadanie fragmentov.
        /// </summary>
        private readonly Timer _recieveTimer = new Timer {Interval = 1000, AutoReset = false };

        private readonly Timer _pingTimer = new Timer {Interval = 30000, AutoReset = true};

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
            _pksClient.OnReceivedMessage(_lastMessage.PaketId, false);
            _lastMessage = null;
        }


        internal void RecieveLoop()
        {
            byte[] data;
            try
            {
                _pksClient.Socket.Connect(_pksClient.EndPoint);
                data = Extensions.ConnectedPaket();
                _pksClient.Socket.Client.Send(data, data.Length, SocketFlags.None);
                _pingTimer.Start();

                // Here will be saved information about UDP sender.
                for (;;)
                {
                    _recieveTimer.Start();

                    var task = _pksClient.Socket.ReceiveAsync();
                    while (true)
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
                            {
                                if (!rcv.Equals(_pksClient.EndPoint))
                                {
                                    break;
                                }
                            }

                            DecodePaket(bytes, rcv);
                            break;
                        }

                        if (_lastMessage == null)
                        {
                            SendFragments();
                        }
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
                if(exception != null)
                    _pksClient.OnSocketException(exception);
                else
                    _pksClient.OnClientError();
            }
            finally
            {
                _pingTimer.Stop();
                _recieveTimer.Stop();
                _connected = false;
                if (_pksClient?.Socket != null)
                {
                    if (_pksClient.Socket.Client != null && _pksClient.Socket.Client.Connected)
                    {
                        try
                        {
                            data = Extensions.DisconnectedPaket();
                            _pksClient.Socket.Client.Send(data, data.Length, SocketFlags.None);
                        }
                        finally
                        {
                            try
                            {
                                _pksClient.Socket.Client.Disconnect(true);
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                    try
                    {
                        _pksClient.Socket.Close();
                        _pksClient.Socket.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private bool _secondTry;

        private void DecodePaket(byte[] bytes, IPEndPoint count)
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
            {
                return;
            }

            switch (type)
            {
                case Extensions.Type.RetryFragment:
                    ResendFragments(bytes);
                    return;
                case Extensions.Type.SuccessFull:
                    if (ZleId(bytes)) return;
                    _pksClient.OnReceivedMessage(_lastMessage.PaketId, true);
                    _lastMessage = null;
                    return;
                case Extensions.Type.Fail:
                    if (ZleId(bytes)) return;
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
                {
                    return;
                }
                odoslat = _pksClient.Poradovnik.Dequeue();
            }

            var odoslanie = odoslat as PksClient.SpravaNaOdoslanie;
            if (odoslanie != null)
            {

                _lastMessage = new MessageFragments(new PaketId());
                RozdelSpravuNaFragmenty((MessageFragments)_lastMessage, odoslanie);
                foreach (var fragment in ((MessageFragments)_lastMessage).fragments)
                {
                    _pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
                }
            }
            else
            {
                var naOdoslanie = odoslat as PksClient.SuborNaOdoslanie;
                if (naOdoslanie != null)
                {
                    // ToDo
                }
            }
            _recieveTimer.Start();
        }
        private static void RozdelSpravuNaFragmenty(MessageFragments pksClientLastMessage, PksClient.SpravaNaOdoslanie sprava)
        {
            if (sprava.FragmentSize >= sprava.Sprava.Length + 10)
            {
                var fragment = new byte[sprava.Sprava.Length + 10];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                Encoding.UTF8.GetBytes(sprava.Sprava, 0, sprava.Sprava.Length, fragment, Extensions.FragmentDataIndex);
                fragment.CreateChecksum();
                pksClientLastMessage.fragments.Add(fragment);
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
                Encoding.UTF8.GetBytes(sprava.Sprava, 0, fragment.Length - 18, fragment, Extensions.FragmentDataf0Index);
                sprava.Sprava = sprava.Sprava.Substring(fragment.Length - 18);
                fragment.SetFragmentCount((uint)(sprava.Sprava.Length / (sprava.FragmentSize - 14)) + 1);
                fragment.CreateChecksum();

                pksClientLastMessage.fragments.Add(fragment);

                var offset = 0;
                while (sprava.Sprava.Length - offset > sprava.FragmentSize - 14)
                {
                    fragment = new byte[sprava.FragmentSize];
                    fragment[0] = 0x7E;
                    fragment[fragment.Length - 1] = 0x7E;
                    fragment.SetFragmentId(pksClientLastMessage.PaketId);
                    fragment.SetPaketType(Extensions.Type.Message);
                    fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
                    fragment.SetFragmentOrder(order++);
                    Encoding.UTF8.GetBytes(sprava.Sprava, offset, fragment.Length - 14, fragment, Extensions.FragmentDatafIndex);
                    offset += sprava.FragmentSize - 14;
                    fragment.CreateChecksum();
                    pksClientLastMessage.fragments.Add(fragment);
                }

                fragment = new byte[14 + (sprava.Sprava.Length - offset)];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                fragment[Extensions.FragmentTypeIndex] = fragment[Extensions.FragmentTypeIndex].SetFragmented();
                fragment.SetFragmentOrder(order);
                Encoding.UTF8.GetBytes(sprava.Sprava, offset, fragment.Length - 14, fragment, Extensions.FragmentDatafIndex);
                fragment.CreateChecksum();
                pksClientLastMessage.fragments.Add(fragment);
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

            var order = bytes.GetFragmentOrder();
            var file = _lastMessage as FileFragments;
            if (file != null)
            {
                ResendFileFragments(file, order);
                return;
            }
            var message = _lastMessage as MessageFragments;
            if (message != null)
            {
                ResendMessageFragments(message, (int) order);
            }
        }

        private void ResendMessageFragments(MessageFragments message, int order)
        {
            if (message.FragmentCount < order)
                return;

            _pksClient.Socket.Client.Send(message.fragments[order]);
        }

        private void ResendFileFragments(FileFragments file, uint order)
        {
            throw new NotImplementedException();
        }

        private static bool WrongProtocol(byte[] bytes)
        {
            if (bytes.Length < 5)
            {
                return true;
            }

            if (bytes[0] != 0x7E && bytes[bytes.Length - 1] != 0x7E)
            {
                return true;
            }
            return false;
        }

        private bool NoConnection(Extensions.Type type)
        {
            if (_connected) return false;

            if (type != Extensions.Type.Connect)
            {
                if (_secondTry)
                {
                    _pksClient.OnNoServerResponse();
                }
                _secondTry = true;
                return true;
            }

            _pksClient.OnClientConnected();
            _connected = true;
            return false;
        }
    }
}