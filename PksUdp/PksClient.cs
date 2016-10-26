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

        /// <summary>
        /// Local (Listener) Port.
        /// </summary>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="SocketException"/>
        public int? Port
        {
            get { return ((IPEndPoint)Socket?.Client.LocalEndPoint)?.Port; }
            set
            {
                if (!value.HasValue)
                    return;
                Close();
                Init(value.Value);
            }
        }

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
        public PksClient(int port) : this()
        {
            Init(port);
        }

        /// <summary>
        /// Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="SocketException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="OutOfMemoryException"/>
        /// <param name="port">Port that will be used for communication.</param>
        private void Init(int port)
        {
            try
            {
                Socket = new UdpClient(port) { Client = { SendTimeout = 5000, ReceiveTimeout = 50000 } };
            }
            catch (SocketException)
            {
                Socket?.Close();
                throw;
            }
            _thread = new Thread(new ClientThread(this).Loop)
            {
                IsBackground = true,
                Name = $"UdpClient {Port}",
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();
        }

        /// <summary>
        /// Abort recieving thread and close socket.
        /// </summary>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="SocketException"/>
        public void Close()
        {
            _thread?.Abort();
            Socket?.Close();
        }
    }
}
