﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PksUdp.Server
{
    public partial class PksServer
    {
        /// <summary>
        ///     Thread for handling packets.
        /// </summary>
        private Thread _thread;

        /// <summary>
        ///     Local (Listener) UDP Socket.
        /// </summary>
        internal UdpClient Socket;

        /// <summary>
        ///     Create communicator.
        /// </summary>
        public PksServer()
        {
        }

        /// <summary>
        ///     Create communicator than Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="SocketException" />
        /// <exception cref="ThreadStartException" />
        /// <exception cref="OutOfMemoryException" />
        /// <param name="port">Port that will be used for communication.</param>
        public PksServer(int port) : this()
        {
            Init(port);
        }

        /// <summary>
        ///     Local (Listener) Port.
        /// </summary>
        /// <exception cref="ThreadStartException" />
        /// <exception cref="SocketException" />
        public int? Port
        {
            get { return ((IPEndPoint) Socket?.Client.LocalEndPoint)?.Port; }
            set
            {
                if (!value.HasValue)
                    return;
                Close();
                Init(value.Value);
            }
        }

        private static byte[] CreateFragment(Extensions.Type type, PaketId id, uint fragmentOrder)
        {
            var data = new byte[14];
            data[0] = 0x7E;
            data[13] = 0x7E;
            data.SetPaketType(type);
            data.SetFragmentId(id);
            data.SetFragmentOrder(fragmentOrder);
            data.CreateChecksum();
            return data;
        }

        internal static byte[] CancelPaket(PaketId id) => CreateFragment(Extensions.Type.Cancel, id, 0);
        internal static byte[] FailPaket(PaketId id) => CreateFragment(Extensions.Type.Fail, id, 0);
        internal static byte[] FailPaket() => CreateFragment(Extensions.Type.Fail, new PaketId(0, 0), 0);

        internal static byte[] SuccessPaket(PaketId id, uint fragmentCount)
            => CreateFragment(Extensions.Type.SuccessFull, id, fragmentCount);

        internal static byte[] RetryPaket() => RetryFragment(new PaketId(0, 0), 0);

        internal static byte[] RetryFragment(PaketId id, uint fragmentOrder)
            => CreateFragment(Extensions.Type.RetryFragment, id, fragmentOrder);

        /// <summary>
        ///     Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="SocketException" />
        /// <exception cref="ThreadStartException" />
        /// <exception cref="OutOfMemoryException" />
        /// <param name="port">Port that will be used for communication.</param>
        private void Init(int port)
        {
            try
            {
                Socket = new UdpClient(port) {Client = {SendTimeout = 5000, ReceiveTimeout = 45000}};
            }
            catch (SocketException)
            {
                Socket?.Close();
                throw;
            }
            _thread = new Thread(new ServerThread(this).Loop)
            {
                IsBackground = true,
                Name = $"UdpServer {Port}",
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();
        }

        /// <summary>
        ///     Abort recieving thread and close socket.
        /// </summary>
        /// <exception cref="ThreadStartException" />
        /// <exception cref="SocketException" />
        public void Close()
        {
            _thread?.Abort();
            Socket?.Close();
        }
    }
}