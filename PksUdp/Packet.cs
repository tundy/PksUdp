namespace PksUdp
{
    public class Packet
    {
        /// <summary>
        /// Total count of Fragments used for this message.
        /// </summary>
        public uint FragmentsCount { internal set; get; }

        /// <summary>
        /// Maximum length used for fragments.
        /// </summary>
        public int FragmentLength { internal set; get; }

        /// <summary>
        /// Determine if Message was recieved successfully.
        /// </summary>
        public bool Error { internal set; get; }

        public PaketId PaketId { internal set; get; }

        public Packet()
        {
            
        }

        public Packet(Packet packet)
        {
            FragmentsCount = packet.FragmentsCount;
            FragmentLength = packet.FragmentLength;
            Error = packet.Error;
        }
    }
}