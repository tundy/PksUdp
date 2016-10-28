using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PksUdp
{
    public partial class PksClient
    {
        public delegate void ReceivedMessageHandler(PaketId id, bool success);
        public delegate void ServerHandler();


        public event ReceivedMessageHandler ReceivedMessage;
        internal virtual void OnReceivedMessage(PaketId id, bool success)
        {
            ReceivedMessage?.Invoke(id, success);
        }

        public event ServerHandler ClientConnected;
        internal virtual void OnClientConnected()
        {
            ClientConnected?.Invoke();
        }

        public event ServerHandler ServerTimedOut;
        internal virtual void OnServerTimedOut()
        {
            ServerTimedOut?.Invoke();
            Close();
        }

        public event ServerHandler ServerClosedConnection;
        internal virtual void OnServerClosedConnection()
        {
            ServerClosedConnection?.Invoke();
            Close();
        }
    }
}
