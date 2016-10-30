using System.Collections.Generic;
using System.Linq;

namespace PksUdp
{
    internal abstract class PaketFragments
    {
        internal readonly List<byte[]> Fragments = new List<byte[]>();
        internal readonly PaketId PaketId;

        protected PaketFragments(PaketId paketId)
        {
            PaketId = paketId;
        }

        internal uint FragmentCount => (uint) Fragments.LongCount();
        internal uint FragmentLength => (uint) Fragments.First().LongLength;
    }
}