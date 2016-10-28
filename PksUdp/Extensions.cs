using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PksUdp
{
    internal static class Extensions
    {
        internal static byte[] ConnectedPaket()
        {
            var data = new byte[5];
            data[0] = 0x7E;
            data[4] = 0x7E;
            data.SetPaketType(Type.Connect);
            data.CreateChecksum();
            return data;
        }

        internal static byte[] DisconnectedPaket()
        {
            var data = new byte[5];
            data[0] = 0x7E;
            data[4] = 0x7E;
            data.SetPaketType(Type.Disconnect);
            data.CreateChecksum();
            return data;
        }

        private const int FragmentTypeIndex = 1;
        private const int FragmentIdIndex = 2;
        private const int FragmentOrderIndex = 7;
        private const int FragmentCountIndex = 11;
        internal const int FragmentDataIndex = 7;
        internal const int FragmentDataf0Index = 11;
        internal const int FragmentDatafIndex = 15;

        public static int ToUnixTime(this DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
            return (int)(epoch - dateTime).TotalSeconds;
        }

        public static DateTime ToDateTime(this int unixTime)
        {
            var epoch = new DateTime(1970,1,1,0,0,0,0).ToLocalTime();
            return epoch.AddSeconds(unixTime);
        }

        public static void SetPaketType(this byte[] data, Type type)
        {
            data[1] = (byte)type;
        }

        public static Type GetPaketType(this byte[] data) => data[FragmentTypeIndex].GetPaketType();

        public static Type GetPaketType(this byte type)
        {
            try
            {
                return (Type)(type & ~0x80);
            }
            catch (Exception)
            {
                return Type.Nothing;
            }
        }

        public enum Type : byte
        {
            /// <summary>
            /// Unknown type.
            /// </summary>
            Nothing = 0x80,
            /// <summary>
            /// CLient connected.
            /// </summary>
            Connect = 0x0,
            /// <summary>
            /// Keeping conection alive.
            /// </summary>
            Ping = 0x1,
            /// <summary>
            /// Text message.
            /// </summary>
            Message = 0x2,
            /// <summary>
            /// File transfer
            /// </summary>
            File = 0x3,
            /// <summary>
            /// Client has disconnected from server
            /// </summary>
            Disconnect = 0x4,
            /// <summary>
            /// Corrupted paket (first fragment), Resend.
            /// </summary>
            RetryFragment = 0x5,
            /// <summary>
            /// Message successfully recieved/sent.
            /// </summary>
            SuccessFull = 0x6,
            /// <summary>
            /// Stop sending paket.
            /// </summary>
            Fail = 0x7,
            /// <summary>
            /// Paket failed.
            /// </summary>
            Cancel = 0x7
        }

        internal static short CalculateChecksum(this byte[] data)
        {
            const int div = 0xB5AD;
            if (data.Length <= 4)
            {
                return 0;
            }
            if (data.Length == 5)
            {
                return (short)(data[1] ^ (div & 0xFF));
            }

            var result = 0;
            for (var i = 1; i < data.Length - 4; i++)
            {
                result ^= ((data[i] << 8) | data[i + 1]) ^ div;
            }

            return (short)(result & 0xffff);
        }
        public static void CreateChecksum(this byte[] data)
        {
            data.SetChecksum(data.CalculateChecksum());
        }
        private static void SetChecksum(this byte[] data, short crc)
        {
            data[data.Length - 2] = (byte)crc;
            data[data.Length - 3] = (byte)(crc >> 8);
        }

        public static bool CheckChecksum(this byte[] data) => data.CompareChecksum(data.CalculateChecksum());

        /// <summary>
        /// Returns if checksums in byte array is same as expected checksum.
        /// </summary>
        /// <param name="data">Byte array with data and checksum</param>
        /// <param name="crc">Expected checksum.</param>
        /// <returns>If checksums are same</returns>
        public static bool CompareChecksum(this byte[] data, short crc) => data[data.Length - 2] == (byte) crc && data[data.Length - 3] == (byte) (crc >> 8);

        public static void SetFragmentId(this byte[] data, PaketId id)
        {
            data[FragmentOrderIndex] = (byte)((id.UnixTime >> 24) & 0xFF);
            data[FragmentOrderIndex + 1] = (byte)((id.UnixTime >> 16) & 0xFF);
            data[FragmentOrderIndex + 2] = (byte)((id.UnixTime >> 8) & 0xFF);
            data[FragmentOrderIndex + 3] = (byte)(id.UnixTime & 0xFF);
            data[FragmentOrderIndex + 3] = id.Id;
        }
        public static PaketId GetFragmentId(this byte[] data)
        {
            return new PaketId(data, FragmentIdIndex);
        }

        public static void SetFragmentOrder(this byte[] data, uint id)
        {
            data[FragmentOrderIndex] = (byte)((id >> 24) & 0xFF);
            data[FragmentOrderIndex + 1] = (byte)((id >> 16) & 0xFF);
            data[FragmentOrderIndex + 2] = (byte)((id >> 8) & 0xFF);
            data[FragmentOrderIndex + 3] = (byte)(id & 0xFF);
        }
        public static uint GetFragmentOrder(this byte[] data) => (uint)((data[FragmentOrderIndex] << 24) | (data[FragmentOrderIndex + 1] << 16) | (data[FragmentOrderIndex + 2] << 8) | data[FragmentOrderIndex + 3]);

        public static void SetFragmentCount(this byte[] data, uint count)
        {
            data[FragmentCountIndex] = (byte)((count >> 24) & 0xFF);
            data[FragmentCountIndex + 1] = (byte)((count >> 16) & 0xFF);
            data[FragmentCountIndex + 2] = (byte)((count >> 8) & 0xFF);
            data[FragmentCountIndex + 3] = (byte)(count & 0xFF);
        }
        public static uint GetFragmentCount(this byte[] data) => (uint)((data[FragmentCountIndex] << 24) | (data[FragmentCountIndex + 1] << 16) | (data[FragmentCountIndex + 2] << 8) | data[FragmentCountIndex + 3]);

        private static bool HasFlag(this byte b, byte flag) => (b & flag) == flag;
        public static bool IsFragmented(this byte[] data) => data[FragmentTypeIndex].HasFlag(0x80);
        public static byte SetFragmented(this byte b) => (byte)(b | 0x80);
    }
}
