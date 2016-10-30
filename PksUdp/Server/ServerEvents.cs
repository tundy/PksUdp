using System;
using System.Net;

namespace PksUdp.Server
{
    public partial class PksServer
    {
        public delegate void ReceivedMessageHandler(IPEndPoint endPoint, Message message);
        public delegate void ReceivedFileHandler(IPEndPoint endPoint, FilePacket file);
        public delegate void ClientHandler(IPEndPoint endPoint);
        public delegate void Error(Exception e);
        public delegate void BufferHandler(IPEndPoint endpoint, PaketId id, uint loaded, uint? total);

        public event Error ServerDown;
        internal virtual void OnServerDown(Exception e)
        {
            ServerDown?.Invoke(e);
        }

        public event BufferHandler Buffering;
        internal virtual void OnBuffering(IPEndPoint endPoint, PaketId id, uint loaded, uint? total)
        {
            Buffering?.Invoke(endPoint, id, loaded, total);
        }

        public event ReceivedMessageHandler ReceivedMessage;
        internal virtual void OnReceivedMessage(IPEndPoint endPoint, Message message)
        {
            ReceivedMessage?.Invoke(endPoint, message);
        }


        public event ReceivedFileHandler ReceivedFile;
        internal virtual void OnReceivedFile(IPEndPoint endPoint, FilePacket file)
        {
            ReceivedFile?.Invoke(endPoint, file);
        }

        public event ClientHandler ClientConnected;
        internal virtual void OnClientConnected(IPEndPoint endPoint)
        {
            ClientConnected?.Invoke(endPoint);
        }

        public event ClientHandler ClientDisconnected;
        internal virtual void OnClientDisconnected(IPEndPoint endPoint)
        {
            ClientDisconnected?.Invoke(endPoint);
        }

        public event ClientHandler ClientTimedOut;
        internal virtual void OnClientTimedOut(IPEndPoint endPoint)
        {
            ClientTimedOut?.Invoke(endPoint);
        }
    }
}
