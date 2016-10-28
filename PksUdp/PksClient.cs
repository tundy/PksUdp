using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PksUdp
{
    public partial class PksClient
    {
        internal PaketFragments lastMessage = null;

        /// <summary>
        /// Local (Listener) UDP Socket.
        /// </summary>
        internal UdpClient Socket;
        /// <summary>
        /// Thread for handling packets.
        /// </summary>
        private Thread _thread;

        private int? _lastPort;
        /// <summary>
        /// Local (Listener) Port.
        /// </summary>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="SocketException"/>
        public int? Port
        {
            get { return ((IPEndPoint)Socket?.Client?.LocalEndPoint)?.Port; }
            set
            {
                Close();
                _lastPort = value;
                Init();
            }
        }

        internal IPEndPoint endPoint;

        /// <summary>
        /// Create communicator.
        /// </summary>
        public PksClient()
        {
        }

        /// <summary>
        /// Create communicator than Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="SocketException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="OutOfMemoryException"/>
        /// <param name="port">Port that will be used for communication.</param>
        public PksClient(int? port) : this()
        {
            Port = port;
        }

        public void Connect(string ip, int port)
        {
            endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Connect();
        }

        public void Connect(IPAddress ip, int port)
        {
            endPoint = new IPEndPoint(ip, port);
            Connect();
        }

        private void Connect()
        {
            Close();
            Init();

            _thread = new Thread(new ClientThread(this).Loop)
            {
                IsBackground = true,
                Name = $"UdpClient {Port} - {endPoint}",
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();
        }

        public void Connect(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;
            Connect();
        }

        /// <summary>
        /// Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="SocketException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="OutOfMemoryException"/>
        /// <param name="port">Port that will be used for communication.</param>
        private void Init()
        {
            if (!_lastPort.HasValue)
            {
                Socket = new UdpClient();
                return;
            }

            try
            {
                Socket = new UdpClient(_lastPort.Value) { Client = { SendTimeout = 5000, ReceiveTimeout = 60000 } };
            }
            catch (SocketException)
            {
                Socket?.Close();
                throw;
            }
        }

        /// <summary>
        /// Abort recieving thread and close socket.
        /// </summary>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="SocketException"/>
        public void Close()
        {
            if(_thread != null && _thread.IsAlive)
                _thread.Abort();
            Socket?.Close();
        }
    }
}
