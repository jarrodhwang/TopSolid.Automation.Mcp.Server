namespace TopSolid.Automation.Mcp.Contracts
{
    /// <summary>Display-only precision and bounded transport; never document/operation modeling tolerances.</summary>
    public static class GraphicPreviewQuality
    {
        public const double LinearToleranceMm = 0.05;
        public const double AngularToleranceDegrees = 5;
        public const int MaximumTriangles = 2000000;
        public const int MaximumStlBytes = 84 + MaximumTriangles * 50;
        public const int MaximumGlbBytes = 128 * 1024 * 1024;
        public const int ChunkBytes = 512 * 1024;
        // Large geometry is streamed in bounded chunks, never a single enormous JSON line.
        public const int MaximumRpcLineCharacters = (84 + 250000 * 50 + 2) / 3 * 4 + 65536;
    }
}
