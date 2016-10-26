using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PksUdp
{
    internal class MessageFragments : PaketFragments
    {
        internal readonly List<byte[]> fragments = new List<byte[]>();
        internal override uint FragmentCount => (uint)fragments.LongCount();
        internal override uint FragmentLength => (uint)fragments.Last().LongLength;

        public MessageFragments(PaketId paketId) : base(paketId)
        {
        }
    }
}
