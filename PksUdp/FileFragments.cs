using System.Collections.Generic;
using System.Linq;

namespace PksUdp
{
    internal class FileFragments : PaketFragments
    {
        internal readonly string Path;

        public FileFragments(string path, PaketId paketId) : base(paketId)
        {
            Path = path;
        }
    }
}