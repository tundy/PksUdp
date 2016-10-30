using System;
using System.Collections.Generic;

namespace PksUdp
{
    public class PaketId
    {
        private static DateTime _lastDate = DateTime.Now;
        private static byte _id;
        public readonly byte Id;
        public readonly int UnixTime;

        public PaketId(IReadOnlyList<byte> data, int index)
        {
            UnixTime = data[index++] << 24;
            UnixTime |= data[index++] << 16;
            UnixTime |= data[index++] << 8;
            UnixTime |= data[index++];
            Id = data[index];
        }

        public PaketId(int unixTime, byte id)
        {
            UnixTime = unixTime;
            Id = id;
        }

        public PaketId()
        {
            var now = DateTime.Now;
            if (_lastDate.Equals(now))
            {
                ++_id;
            }
            else
            {
                _lastDate = now;
                _id = 0;
            }

            UnixTime = (int) _lastDate.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            Id = _id;
        }

        internal bool Equals(PaketId other)
        {
            return (UnixTime == other.UnixTime) && (Id == other.Id);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (UnixTime*397) ^ Id.GetHashCode();
            }
        }
    }
}