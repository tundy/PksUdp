using System;
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
        /// Timer pre kontrolu spojenia.
        /// </summary>
        private readonly System.Timers.Timer _connectionTimer = new System.Timers.Timer
        {
            Interval = 45000,
            AutoReset = false
        };

        /// <summary>
        /// Timer pre znovu vyziadanie fragmentov.
        /// </summary>
        private readonly System.Timers.Timer _recieveTimer = new System.Timers.Timer {Interval = 500, AutoReset = false};

        public ClientThread(PksClient pksClient)
        {
            _pksClient = pksClient;
            _recieveTimer.Elapsed += _recieveTimer_Elapsed;
            _connectionTimer.Elapsed += _connectionTimer_Elapsed;
        }

        private void _connectionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void _recieveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }


        internal void Loop()
        {
            // Here will be saved information about UDP sender.
            for (;;)
            {
                try
                {
                    _connectionTimer.Start();
                    _recieveTimer.Start();

                    var bytes = new byte[30];
                    var count = _pksClient.Socket.Client.Receive(bytes);

                    _recieveTimer.Stop();
                    _connectionTimer.Stop();

                    DecodePaket(bytes, count);
                }
                catch (ThreadAbortException)
                {
                    if(_pksClient.Socket.Client.Connected)
                        _pksClient.Socket.Client.Disconnect(true);
                    return;
                }
                catch (SocketException ex)
                {
                    // ConnectionReset = An existing connection was forcibly closed by the remote host
                    if (ex.SocketErrorCode == SocketError.ConnectionReset || ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        _pksClient.OnServerTimedOut();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private bool _secondTry;

        private void DecodePaket(byte[] bytes, int count)
        {
            if (CheckFragment(bytes, count)) return;

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
                    ResendFragments(bytes, count);
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

        private static bool CheckFragment(byte[] bytes, int count)
        {
            return WrongProtocol(bytes, count) || !bytes.CheckChecksum();
        }

        private void ResendFragments(byte[] bytes, int count)
        {
            if (count < 14)
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

        private static bool WrongProtocol(byte[] bytes, int count)
        {
            if (count < 5)
            {
                return true;
            }

            if (bytes[0] != 0x7E && bytes[count - 1] != 0x7E)
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