using System;

namespace TopSolid.Automation.Mcp.Contracts
{
    /// <summary>Version floors shared by the connection picker, tool catalog and server guard.</summary>
    public static class TopSolidVersionSupport
    {
        // TopSolid encodes 7.18.400.283 as 718400283.
        public const int MinimumSupportedVersion = 718000000;
        // The installed 7.18 distribution does not contain the Cae/Electrode Automating assemblies.
        public const int CaeAndElectrodeMinimumVersion = 720000000;
        public const int ModelingMinimumVersion = 720326000;

        public static bool IsAtLeast(int version, int minimum) => version >= minimum;

        public static string Display(int version)
        {
            if (version <= 0) return "unknown";
            return version / 100000000 + "." + version / 1000000 % 100;
        }

        public static string DisplayFull(int version)
        {
            if (version <= 0) return "unknown";
            return string.Format("{0}.{1}.{2}.{3}", version / 100000000, version / 1000000 % 100, version / 1000 % 1000, version % 1000);
        }
    }
}
