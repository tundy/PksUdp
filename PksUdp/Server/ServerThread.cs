﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PksUdp.Server
{
    internal class ServerThread
    {
        /// <summary>
        /// Maximalny pocet opakovani pre znovu vziadanie fragmentu.
        /// </summary>
        private const int MaxRetry = 10;
        /// <summary>
        /// Aktualny pokus znovu vyziadania fragmentu.
        /// </summary>
        private int _attemp;

        /// <summary>
        /// Udp Socket.
        /// </summary>
        private readonly PksServer _pksServer;

        private readonly object _fragmentLock = new object();
        private readonly object _clientLock = new object();

        /// <summary>
        /// Udaje o klientovi.
        /// </summary>
        private IPEndPoint Client
        {
            get { return _client; }
            set { _pingTimer.Stop(); _client = value; if(value != null) _pingTimer.Start(); }
        }

        /// <summary>
        /// Posledny typ spravy.
        /// </summary>
        private Extensions.Type _lastFragmentType = Extensions.Type.Nothing;
        /// <summary>
        /// Celkovy pocet fragmentov pre spravu.
        /// </summary>
        private long _fragmentCount = -1;
        /// <summary>
        /// Dlzka fragmentu.
        /// </summary>
        private int _fragmentLength = -1;
        /// <summary>
        /// Ulozisko dat pre fragmenty.
        /// </summary>
        private readonly Dictionary<uint, string> _fragments = new Dictionary<uint, string>();
        /// <summary>
        /// Id spravy.
        /// </summary>
        private PaketId _lastId;

        /// <summary>
        /// Timer pre znovu vyziadanie fragmentov.
        /// </summary>
        private readonly Timer _recieveTimer = new Timer { Interval = 500, AutoReset = false};
        private readonly Timer _pingTimer = new Timer { Interval = 30000, AutoReset = true};
        private IPEndPoint _client;

        internal ServerThread(PksServer pksServer)
        {
            _pksServer = pksServer;
            _recieveTimer.Elapsed += _recieveTimer_Elapsed;
            _pingTimer.Elapsed += _pingTimer_Elapsed; ;
        }

        private void _pingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            PingClient();
        }

        /// <summary>
        /// Ping client to keep connection alive.
        /// </summary>
        private void PingClient()
        {
            lock (_clientLock)
            {
                if (Client == null)
                {
                    return;
                }

                var data = Extensions.PingPaket();
                _pksServer.Socket.Send(data, data.Length, Client);
            }
        }

        private void _recieveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_attemp++ < MaxRetry)
            {
                AskForFragments();
                return;
            }
            PaketFailed((uint) _fragmentCount, _lastId, Client);
        }

        /// <summary>
        /// Znovu vyziadaj vsetky chybajuce fragmenty.
        /// </summary>
        private async void AskForFragments()
        {
            var sendList = new List<Task<int>>();

            lock (_clientLock)
            {
                if (Client == null)
                {
                    return;
                }

                lock (_fragmentLock)
                {
                    if (_fragments.LongCount() == 0 || _fragmentCount == -1)
                    {
                        var data = PksServer.RetryPaket();
                        sendList.Add(_pksServer.Socket.SendAsync(data, data.Length, Client));
                    }
                    else
                    {
                        var missing = _fragmentCount - _fragments.LongCount();
                        for (long i = 0; missing > 0 && i < _fragmentCount; i++)
                        {
                            if (_fragments.ContainsKey((uint) i)) continue;
                            var data = PksServer.RetryFragment(_lastId, (uint)i);
                            sendList.Add(_pksServer.Socket.SendAsync(data, data.Length, Client));
                            --missing;
                        }
                    }
                }
            }

            foreach (var task in sendList)
            {
                await task;
            }
        }

        internal void Loop()
        {
            // Here will be saved information about UDP sender.
            var sender = new IPEndPoint(IPAddress.Any, 0);

            for (;;)
            {
                try
                {
                    _recieveTimer.Start();

                    var bytes = _pksServer.Socket.Receive(ref sender);

                    if (FilterClients(sender))
                    {
                        continue;
                    }

                    _recieveTimer.Stop();

                    if (!IsFragmentCorrect(bytes))
                    {
                        bytes = null;
                        lock (_clientLock)
                        {
                            if (_client == null)
                            {
                                bytes = PksServer.FailPaket();
                            }
                        }
                        if (bytes != null)
                        {
                            _pksServer.Socket.SendAsync(bytes, bytes.Length, sender);
                            continue;
                        }
                        if (_lastFragmentType == Extensions.Type.Nothing)
                        {
                            AskForFragments();
                        }
                        continue;
                    }

                    var type = bytes.GetPaketType();

                    if (SpracujPripojenieOdpojenieKlienta(type, sender))
                    {
                        continue;
                    }

                    if (_lastFragmentType != Extensions.Type.Nothing && type != _lastFragmentType)
                    {
                        continue;
                    }
                    _lastFragmentType = type;

                    if (type == Extensions.Type.Ping)
                    {
                        continue;
                    }

                    var id = bytes.GetFragmentId();
                    if (_lastId != null && _lastId != id)
                    {
                        continue;
                    }
                    _lastId = id;

                    switch (type)
                    {
                        case Extensions.Type.Message:
                            SpracujSpravu(bytes, id, sender);
                            break;
                        case Extensions.Type.File:
                            SpracujSubor(bytes, id, sender);
                            _lastFragmentType = Extensions.Type.Nothing;
                            break;
                        default:
                            _lastFragmentType = Extensions.Type.Nothing;
                            break;
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    // ConnectionReset = An existing connection was forcibly closed by the remote host
                    if (ex.SocketErrorCode == SocketError.ConnectionReset || ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        lock (_clientLock)
                        {
                            if (Client == null)
                            {
                                continue;
                            }
                            Client = null;

                        }
                        _pksServer.OnClientTimedOut(sender);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private void SpracujSubor(byte[] bytes, PaketId id, IPEndPoint sender)
        {
            _lastFragmentType = Extensions.Type.Nothing;
            //ResetCounter();
        }

        /// <summary>
        /// Vrati 'true' pokail netreba fragment dalej srpacovavat.
        /// </summary>
        /// <param name="type">Typ spravy</param>
        /// <param name="client"></param>
        private bool SpracujPripojenieOdpojenieKlienta(Extensions.Type type, IPEndPoint client)
        {
            lock (_clientLock)
            {
                if (type == Extensions.Type.Connect)
                {
                    var data = Extensions.ConnectedPaket();
                    _pksServer.Socket.SendAsync(data, data.Length, client);
                    _pksServer.Socket.SendAsync(data, data.Length, client);
                    Client = client;
                    ResetCounter();
                    _pksServer.OnClientConnected(client);
                    return true;
                }

                if (_client == null)
                {
                    return true;
                }

                if (type != Extensions.Type.Disconnect) return false;

                _pksServer.OnClientDisconnected(client);
                Client = null;
                return true;
            }
        }

        /// <summary>
        /// Vrati 'true' pokial je sprava od ineho pouzivatela.
        /// </summary>
        /// <param name="sender">Pouzivatel</param>
        private bool FilterClients(IPEndPoint sender)
        {
            lock (_clientLock)
            {
                if (Client != null && (!Client.Address.Equals(sender.Address) || !Client.Port.Equals(sender.Port)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Skontroluje ci fragment splna minimalne poziadavky.
        /// </summary>
        /// <param name="bytes">fragment</param>
        private static bool IsFragmentCorrect(byte[] bytes)
        {
            if (bytes.Length < 5)
            {
                return false;
            }

            if (bytes[0] != 0x7E && bytes[0] != 0x7E)
            {
                return false;
            }

            return bytes.CheckChecksum();
        }

        /// <summary>
        /// Spracuj fragment typu Message.
        /// </summary>
        /// <param name="bytes">Fragment</param>
        /// <param name="id">Id spravy</param>
        /// <param name="sender">Client</param>
        private void SpracujSpravu(byte[] bytes, PaketId id, IPEndPoint sender)
        {
            if (!bytes.IsFragmented())
            {
                SpracujNefragmentovanuSpravu(bytes, id, sender);
            }
            else
            {
                SpracujFragmentovanuSpravu(bytes, id, sender);
            }
        }

        /// <summary>
        /// Ulozi si fragment.
        /// </summary>
        /// <param name="bytes">Fragment</param>
        /// <param name="id">Id spravy</param>
        /// <param name="sender">client</param>
        private void SpracujFragmentovanuSpravu(byte[] bytes, PaketId id, IPEndPoint sender)
        {
            var order = bytes.GetFragmentOrder();
            lock (_fragmentLock)
            {
                if (_fragmentLength < bytes.Length)
                    _fragmentLength = bytes.Length;

                if (order == 0)
                {
                    if (!_fragments.ContainsKey(order))
                    {
                        _fragmentCount = bytes.GetFragmentCount() + 1;
                        _fragments.Add(order, Encoding.UTF8.GetString(bytes, Extensions.FragmentDataf0Index,
                            bytes.Length - Extensions.FragmentDataf0Index - 3));
                    }
                }
                else
                {
                    if (!_fragments.ContainsKey(order))
                    {
                        if (_fragments.ContainsKey(0))
                        {
                            if (order >= _fragmentCount)
                            {
                                PaketFailed(order, id, sender);
                                return;
                            }
                        }
                        else
                        {
                            if (order >= _fragmentCount)
                            {
                                _fragmentCount = order + 1;
                            }
                        }

                        _fragments.Add(order, Encoding.UTF8.GetString(bytes, Extensions.FragmentDatafIndex,
                            bytes.Length - Extensions.FragmentDatafIndex - 3));
                    }
                }
            }

            if (_fragments.LongCount() != _fragmentCount) return;

            SpojFragmentySpravy(id, sender);
        }

        /// <summary>
        /// Vysle event o prijati srpavy.
        /// </summary>
        /// <param name="id">Id spravy</param>
        /// <param name="sender">client</param>
        private void SpojFragmentySpravy(PaketId id, IPEndPoint sender)
        {
            var data = PksServer.SuccessPaket(id, (uint)_fragmentCount);
            _pksServer.Socket.SendAsync(data, data.Length, sender);

            var sb = new StringBuilder();
            for (long i = 0; i < _fragments.LongCount(); i++)
            {
                sb.Append(_fragments[(uint) i]);
            }
            _pksServer.OnReceivedMessage(sender, new Message
            {
                Text = sb.ToString(),
                Error = false,
                FragmentsCount = (uint) _fragmentCount,
                FragmentLength = _fragmentLength,
                PaketId = id
            });
            ResetCounter();
        }

        /// <summary>
        /// Vysle event o prijati spravy.
        /// </summary>
        /// <param name="bytes">Fragment</param>
        /// <param name="id">Id spravy</param>
        /// <param name="sender">Client</param>
        private void SpracujNefragmentovanuSpravu(byte[] bytes, PaketId id, IPEndPoint sender)
        {
            var data = PksServer.SuccessPaket(id, (uint) _fragmentCount);
            _pksServer.Socket.SendAsync(data, data.Length, sender);
            _pksServer.OnReceivedMessage(sender, new Message
            {
                Text =
                    Encoding.UTF8.GetString(bytes, Extensions.FragmentDataIndex,
                        bytes.Length - Extensions.FragmentDataIndex - 3),
                Error = false,
                FragmentsCount = 1,
                FragmentLength = bytes.Length,
                PaketId = id
            });
            ResetCounter();
        }

        /// <summary>
        /// Vysle event o neuspesnom prijati spravy a vynuluje pocitadla.
        /// </summary>
        /// <param name="count">Predpokladany pocet fragmentov</param>
        /// <param name="id">Id spravy</param>
        /// <param name="sender">CLient</param>
        private void PaketFailed(uint count, PaketId id, IPEndPoint sender)
        {
            switch (_lastFragmentType)
            {
                case Extensions.Type.Message:
                    _pksServer.OnReceivedMessage(sender, new Message
                    {
                        Error = true,
                        FragmentsCount = count,
                        FragmentLength = _fragmentLength,
                        PaketId = id
                    });
                    break;
                case Extensions.Type.File:
                    _pksServer.OnReceivedFile(sender, new FilePacket
                    {
                        Error = true,
                        FragmentsCount = count,
                        FragmentLength = _fragmentLength,
                        PaketId = id
                    });
                    break;
            }

            ResetCounter();
            var data = PksServer.CancelPaket(id);
            _pksServer.Socket.SendAsync(data, data.Length, sender);
        }

        /// <summary>
        /// Set all values to default.
        /// </summary>
        private void ResetCounter()
        {
            _attemp = 0;
            _fragmentCount = -1;
            _fragmentLength = 0;
            _lastFragmentType = Extensions.Type.Nothing;
            lock (_fragmentLock)
            {
                _fragments.Clear();
            }
        }
    }
}