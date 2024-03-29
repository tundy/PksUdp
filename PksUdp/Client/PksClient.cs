﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PksUdp.Client
{
    public partial class PksClient
    {
        public enum Fragmenty
        {
            ZiadneChybne,
            PrvyChybny,
            VsetkyChybne
        }


        internal readonly Queue<NaOdoslanie> Poradovnik = new Queue<NaOdoslanie>();
        internal readonly object PoradovnikLock = new object();

        private int? _lastPort;

        /// <summary>
        ///     Thread for handling packets.
        /// </summary>
        private Thread _listener;

        internal IPEndPoint EndPoint;

        /// <summary>
        ///     Local (Listener) UDP Socket.
        /// </summary>
        internal UdpClient Socket;

        /// <summary>
        ///     Create communicator.
        /// </summary>
        public PksClient()
        {
        }

        /// <summary>
        ///     Create communicator than Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="System.Net.Sockets.SocketException" />
        /// <exception cref="ThreadStartException" />
        /// <exception cref="OutOfMemoryException" />
        /// <param name="port">Port that will be used for communication.</param>
        public PksClient(int? port) : this()
        {
            Port = port;
        }

        /// <summary>
        ///     Local (Listener) Port.
        /// </summary>
        /// <exception cref="ThreadStartException" />
        /// <exception cref="System.Net.Sockets.SocketException" />
        public int? Port
        {
            get { return ((IPEndPoint) Socket?.Client?.LocalEndPoint)?.Port; }
            set
            {
                Close();
                _lastPort = value;
                Init();
            }
        }

        public void SendMessage(string text, int fragmentSize, Fragmenty error)
        {
            if ((fragmentSize < 20) || (fragmentSize > 65470))
                return;
            lock (PoradovnikLock)
            {
                Poradovnik.Enqueue(new SpravaNaOdoslanie(text, fragmentSize, error));
            }
        }

        public void SendFile(string path, int fragmentSize, Fragmenty error)
        {
            if ((fragmentSize < 20) || (fragmentSize > 65470))
                return;
            lock (PoradovnikLock)
            {
                Poradovnik.Enqueue(new SuborNaOdoslanie(path, fragmentSize, error));
            }
        }

        public void Connect(string ip, int port)
        {
            EndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Connect();
        }

        public void Connect(IPAddress ip, int port)
        {
            EndPoint = new IPEndPoint(ip, port);
            Connect();
        }

        private void Connect()
        {
            Close();
            Init();

            _listener = new Thread(new ClientThread(this).RecieveLoop)
            {
                IsBackground = true,
                Name = $"UdpClient {Port} - {EndPoint}",
                Priority = ThreadPriority.AboveNormal
            };
            _listener.Start();
        }

        public void Connect(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            Connect();
        }

        /// <summary>
        ///     Open UDP socket and start recieving thread.
        /// </summary>
        /// <exception cref="SocketException" />
        /// <exception cref="ThreadStartException" />
        /// <exception cref="OutOfMemoryException" />
        private void Init()
        {
            if (!_lastPort.HasValue)
            {
                Socket = new UdpClient();
                return;
            }

            try
            {
                Socket = new UdpClient(_lastPort.Value) {Client = {SendTimeout = 5000, ReceiveTimeout = 60000}};
            }
            catch (SocketException)
            {
                Socket?.Close();
                throw;
            }
        }

        /// <summary>
        ///     Abort recieving thread and close socket.
        /// </summary>
        /// <exception cref="ThreadStartException" />
        /// <exception cref="System.Net.Sockets.SocketException" />
        public void Close()
        {
            if ((_listener != null) && _listener.IsAlive)
            {
                _listener.Abort();
                _listener = null;
            }

            if (Socket != null)
            {
                if ((Socket.Client != null) && Socket.Client.Connected)
                    try
                    {
                        var data = Extensions.DisconnectedPaket();
                        Socket.Client.Send(data, data.Length, SocketFlags.None);
                    }
                    catch
                    {
                        // ignored
                    }
                try
                {
                    Socket.Close();
                }
                catch
                {
                    // ignored
                }
                Socket = null;
            }

            lock (PoradovnikLock)
            {
                Poradovnik.Clear();
            }
        }

        internal abstract class NaOdoslanie
        {
            internal readonly Fragmenty Error;
            internal readonly int FragmentSize;

            protected NaOdoslanie(int fragmentSize, Fragmenty error)
            {
                FragmentSize = fragmentSize;
                Error = error;
            }
        }

        internal class SpravaNaOdoslanie : NaOdoslanie
        {
            internal string Sprava;

            public SpravaNaOdoslanie(string sprava, int fragmentSize, Fragmenty error) : base(fragmentSize, error)
            {
                Sprava = sprava;
            }
        }

        internal class SuborNaOdoslanie : NaOdoslanie
        {
            internal readonly string Path;

            public SuborNaOdoslanie(string path, int fragmentSize, Fragmenty error) : base(fragmentSize, error)
            {
                Path = path;
            }
        }
    }
}