namespace PksUdp
{
    internal class FileFragments : PaketFragments
    {
        internal readonly string Path;

        public FileFragments(string path, uint fragmentCount, uint fragmentLength, PaketId paketId) : base(paketId)
        {
            Path = path;
            FragmentCount = fragmentCount;
            FragmentLength = fragmentLength;
        }

        internal override uint FragmentCount { get; }

        internal override uint FragmentLength { get; }
    }
}