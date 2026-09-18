namespace TopSolid.Automation.Mcp.Contracts
{
    /// <summary>Display-only precision and bounded transport; never document/operation modeling tolerances.</summary>
    public static class GraphicPreviewQuality
    {
        public const double LinearToleranceMm = 0.05;
        public const double AngularToleranceDegrees = 5;
        public const int MaximumTriangles = 250000;
        public const int MaximumStlBytes = 84 + MaximumTriangles * 50;
        public const int MaximumRpcLineCharacters = (MaximumStlBytes + 2) / 3 * 4 + 65536;
    }
}
