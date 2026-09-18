using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class GraphicPreviewExportOptions
    {
        // These keys/values are exposed by the installed 7.20 STL exporter. No global exporter/document settings are written.
        internal static bool TryStl(IEnumerable<KeyValue> source, out List<KeyValue> options)
        {
            var available = source.ToList(); options = available;
            if (new[] { "LINEAR_TOLERANCE", "ANGULAR_TOLERANCE", "WRITE_MODE", "USER_UNIT_SET", "USER_UNIT", "AGGREGATES_SHAPES" }
                .Any(key => !available.Any(o => o.Key == key))) return false;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                switch (option.Key)
                {
                    case "LINEAR_TOLERANCE": option.Value = (GraphicPreviewQuality.LinearToleranceMm / 1000).ToString("R", CultureInfo.InvariantCulture); break;
                    case "ANGULAR_TOLERANCE": option.Value = (GraphicPreviewQuality.AngularToleranceDegrees * Math.PI / 180).ToString("R", CultureInfo.InvariantCulture); break;
                    case "WRITE_MODE": option.Value = "0"; break; // Binary STL; installed exporter mode 1 is ASCII.
                    case "USER_UNIT_SET": case "SIMPLIFY_ASSEMBLY_STRUCTURE": case "AGGREGATES_SHAPES": option.Value = "True"; break;
                    case "USER_UNIT": option.Value = "MILLIMETER"; break;
                    case "MAX_FACET_LENGTH": option.Value = "0"; break;
                    case "REFERENCE_FRAME": case "REPRESENTATION_ID": option.Value = ""; break;
                }
                options[i] = option;
            }
            return true;
        }
    }
}
