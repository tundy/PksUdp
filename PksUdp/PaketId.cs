using System;
using System.Collections.Generic;

namespace PksUdp
{
    public class PaketId
    {
        public readonly int UnixTime;
        public readonly byte Id;

        internal bool Equals(PaketId other)
        {
            return UnixTime == other.UnixTime && Id == other.Id;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (UnixTime * 397) ^ Id.GetHashCode();
            }
        }

        public PaketId(IReadOnlyList<byte> data, int index)
        {
            UnixTime = (data[index++] >> 24) & 0xFF;
            UnixTime |= (data[index++] >> 16) & 0xFF;
            UnixTime |= (data[index++] >> 8) & 0xFF;
            UnixTime |= data[index++] & 0xFF;
            Id = data[index];
        }

        public PaketId(IReadOnlyList<byte> data)
        {
            UnixTime = (data[0] >> 24) & 0xFF;
            UnixTime |= (data[1] >> 16) & 0xFF;
            UnixTime |= (data[2] >> 8) & 0xFF;
            UnixTime |= data[3] & 0xFF;
            Id = data[4];
        }

        public PaketId(int unixTime, byte id)
        {
            UnixTime = unixTime;
            Id = id;
        }

        private static DateTime _lastDate = DateTime.Now;
        private static byte _id;

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

            UnixTime = (int)_lastDate.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            Id = _id;
        }
    }
}