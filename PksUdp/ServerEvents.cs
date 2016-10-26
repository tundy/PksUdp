using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PksUdp
{
    public partial class PksServer
    {
        public delegate void ReceivedMessageHandler(IPEndPoint endPoint, Message message);
        public delegate void ReceivedFileHandler(IPEndPoint endPoint, FilePacket file);
        public delegate void ClientHandler(IPEndPoint endPoint);


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
