namespace PksUdp
{
    internal abstract class PaketFragments
    {
        internal readonly PaketId PaketId;

        protected PaketFragments(PaketId paketId)
        {
            PaketId = paketId;
        }

        internal abstract uint FragmentCount { get; }
        internal abstract uint FragmentLength { get; }
    }
}