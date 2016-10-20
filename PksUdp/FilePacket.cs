using System.IO;

namespace PksUdp
{
    /// <summary>
    /// 
    /// </summary>
    public class FilePacket : Packet
    {
        public FileInfo FileInfo { internal set; get; }
    }
}