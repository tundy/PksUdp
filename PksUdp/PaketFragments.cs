using System.Collections.Generic;
using System.Linq;

namespace PksUdp
{
    internal abstract class PaketFragments
    {
        internal readonly PaketId PaketId;


        internal readonly List<byte[]> fragments = new List<byte[]>();
        internal uint FragmentCount => (uint)fragments.LongCount();
        internal uint FragmentLength => (uint)fragments.First().LongLength;

        protected PaketFragments(PaketId paketId)
        {
            PaketId = paketId;
        }
    }
}