using System.Net.Sockets;

namespace PksUdp.Client
{
    public partial class PksClient
    {
        public delegate void ReceivedMessageHandler(PaketId id, bool success);
        public delegate void ServerHandler();
        public delegate void SocketExceptionHandler(SocketException e);

        public event ReceivedMessageHandler ReceivedMessage;
        internal virtual void OnReceivedMessage(PaketId id, bool success)
        {
            ReceivedMessage?.Invoke(id, success);
        }

        public event ReceivedMessageHandler ReceivedFile;
        internal virtual void OnReceivedFile(PaketId id, bool success)
        {
            ReceivedFile?.Invoke(id, success);
        }

        public event ServerHandler ClientConnected;
        internal virtual void OnClientConnected()
        {
            ClientConnected?.Invoke();
        }

        public event ServerHandler NoServerResponse;
        internal virtual void OnNoServerResponse()
        {
            NoServerResponse?.Invoke();
        }

        public event ServerHandler ClientError;
        internal virtual void OnClientError()
        {
            ClientError?.Invoke();
            Close();
        }

        public event SocketExceptionHandler SocketException;
        internal virtual void OnSocketException(SocketException e)
        {
            SocketException?.Invoke(e);
            Close();
        }
    }
}
