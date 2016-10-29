using System.Collections.Generic;
using System.Linq;

namespace PksUdp
{
    internal class MessageFragments : PaketFragments
    {
        internal readonly List<byte[]> fragments = new List<byte[]>();
        internal override uint FragmentCount => (uint)fragments.LongCount();
        internal override uint FragmentLength => (uint)fragments.First().LongLength;

        public MessageFragments(PaketId paketId) : base(paketId)
        {
        }
    }
}
