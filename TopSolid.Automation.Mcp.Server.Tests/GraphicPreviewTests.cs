using System;
using System.Globalization;
using System.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void GraphicPreview()
        {
            var source = new[] { new KeyValue("LINEAR_TOLERANCE", "0.0002"), new KeyValue("ANGULAR_TOLERANCE", "0.261799387799149"),
                new KeyValue("WRITE_MODE", "1"), new KeyValue("USER_UNIT_SET", "False"), new KeyValue("USER_UNIT", "INCH"),
                new KeyValue("AGGREGATES_SHAPES", "False"), new KeyValue("REFERENCE_FRAME", "old-frame") };
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(GraphicPreviewExportOptions.TryStl(source, out var prepared), "Native precision export options rejected");
                var values = prepared.ToDictionary(k => k.Key, k => k.Value);
                Check(double.Parse(values["LINEAR_TOLERANCE"], CultureInfo.InvariantCulture) == .00005, "Preview linear tolerance must be 0.05 mm in SI");
                Check(Math.Abs(double.Parse(values["ANGULAR_TOLERANCE"], CultureInfo.InvariantCulture) - Math.PI / 36) < 1e-15, "Preview angular tolerance must be 5 degrees in radians");
                Check(values["WRITE_MODE"] == "0" && values["USER_UNIT_SET"] == "True" && values["USER_UNIT"] == "MILLIMETER", "Binary STL/mm contract changed");
                Check(values["AGGREGATES_SHAPES"] == "True" && values["REFERENCE_FRAME"] == "", "Preview may export multiple files or a non-default frame");
                Check(source[0].Value == "0.0002" && source[3].Value == "False", "Preview modified shared exporter defaults");
                Check(!GraphicPreviewExportOptions.TryStl(source.Skip(1), out _), "Exporter without tolerance support falsely advertised precision");
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
