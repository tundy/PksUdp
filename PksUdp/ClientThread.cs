﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;

namespace PksUdp
{
    internal class ClientThread
    {
        /// <summary>
        /// Udp Socket.
        /// </summary>
        private readonly PksClient _pksClient;

        private bool _connected = false;

        /// <summary>
        /// Timer pre znovu vyziadanie fragmentov.
        /// </summary>
        private readonly System.Timers.Timer _recieveTimer = new System.Timers.Timer {Interval = 500, AutoReset = false};
        private readonly System.Timers.Timer _pingTimer = new System.Timers.Timer {Interval = 30000, AutoReset = true};

        public ClientThread(PksClient pksClient)
        {
            _pksClient = pksClient;
            _recieveTimer.Elapsed += _recieveTimer_Elapsed;
            _pingTimer.Elapsed += _pingTimer_Elapsed; ;
        }

        private void _pingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Ping();
        }

        private void Ping()
        {
            var data = Extensions.PingPaket();
            _pksClient.Socket.Client.Send(data, data.Length, SocketFlags.None);
        }

        private void _recieveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_pksClient.lastMessage == null) return;
            _pksClient.OnReceivedMessage(_pksClient.lastMessage.PaketId, false);
            _pksClient.lastMessage = null;
        }


        internal void Loop()
        {
            _pksClient.Socket.Connect(_pksClient.endPoint);
            var data = Extensions.ConnectedPaket();
            _pksClient.Socket.Client.Send(data, data.Length, SocketFlags.None);
            _pingTimer.Start();
            var rcv = _pksClient.endPoint;

            // Here will be saved information about UDP sender.
            for (;;)
            {
                try
                {
                    _recieveTimer.Start();

                    var bytes = _pksClient.Socket.Receive(ref rcv);

                    if (_connected)
                    {
                        if (!rcv.Equals(_pksClient.endPoint))
                        {
                            continue;
                        }
                    }

                    _recieveTimer.Stop();

                    DecodePaket(bytes, rcv);
                }
                catch (ThreadAbortException)
                {
                    if (_pksClient.Socket.Client == null || !_pksClient.Socket.Client.Connected) return;
                    try
                    {
                        data = Extensions.DisconnectedPaket();
                        _pksClient.Socket.Client.Send(data, data.Length, SocketFlags.None);
                    }
                    finally 
                    {
                        _pksClient.Socket.Client.Disconnect(true);
                    }
                    return;
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.TimedOut:
                            _pksClient.OnServerTimedOut();
                            return;
                        // ConnectionReset = An existing connection was forcibly closed by the remote host
                        case SocketError.ConnectionReset:
                            _pksClient.OnServerClosedConnection();
                            return;
                        default:
                            throw;
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

            if (_pksClient.lastMessage == null)
            {
                return;
            }

            var id = bytes.GetFragmentId();

            if (!id.Equals(_pksClient.lastMessage.PaketId))
            {
                return;
            }

            switch (type)
            {
                case Extensions.Type.Ping:
                    return;
                case Extensions.Type.RetryFragment:
                    ResendFragments(bytes);
                    return;
                case Extensions.Type.SuccessFull:
                    _pksClient.OnReceivedMessage(_pksClient.lastMessage.PaketId, true);
                    _pksClient.lastMessage = null;
                    return;
                case Extensions.Type.Fail:
                    _pksClient.OnReceivedMessage(_pksClient.lastMessage.PaketId, false);
                    _pksClient.lastMessage = null;
                    return;
                default:
                    return;
            }
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
            var file = _pksClient.lastMessage as FileFragments;
            if (file != null)
            {
                ResendFileFragments(file, order);
                return;
            }
            var message = _pksClient.lastMessage as MessageFragments;
            if (message != null)
            {
                ResendMessageFragments(message, (int)order);
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
                    _pksClient.OnServerTimedOut();
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