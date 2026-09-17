using System;
using System.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static int SketchReadProbe()
        {
            using (var gateway = new AutomationGateway())
            {
                gateway.ConnectModule("kernel");
                var doc = TopSolidHost.Documents.EditedDocument;
                var sketch = TopSolidHost.Sketches2D.GetSketches(doc).First();
                foreach (var id in TopSolidHost.Sketches2D.GetSegments(sketch).Take(5))
                {
                    Console.WriteLine("Segment " + AutomationValues.Json(id));
                    void Read(string name, Func<object> query) { try { Console.WriteLine(name + ": " + AutomationValues.Json(query())); } catch (Exception ex) { Console.WriteLine(name + ": " + ex.Message); } }
                    Read("GetSegmentCurveType", () => TopSolidHost.Sketches2D.GetSegmentCurveType(id));
                    Read("GetSegmentVertices", () => { TopSolidHost.Sketches2D.GetSegmentVertices(id, out var a, out var b); return new[] { a, b }; });
                    Read("GetSegmentCurveRange", () => { TopSolidHost.Sketches2D.GetSegmentCurveRange(id, out var a, out var b); return new[] { a, b }; });
                    Read("GetSegmentRange", () => { TopSolidHost.Sketches2D.GetSegmentRange(id, out var a, out var b); return new[] { a, b }; });
                    Read("GetSegmentPoint(0)", () => TopSolidHost.Sketches2D.GetSegmentPoint(id, 0));
                    Read("IsSegmentConstruction", () => TopSolidHost.Sketches2D.IsSegmentConstruction(id));
                    Read("IsSegmentReversed", () => TopSolidHost.Sketches2D.IsSegmentReversed(id));
                    Read("GetSegmentCenter", () => TopSolidHost.Sketches2D.GetSegmentCenter(id));
                    if (TopSolidHost.Sketches2D.GetSegmentCurveType(id) == CurveType.Line)
                        Read("GetSegmentLineCurve", () => { TopSolidHost.Sketches2D.GetSegmentLineCurve(id, out var axis); return axis; });
                    else if (TopSolidHost.Sketches2D.GetSegmentCurveType(id) == CurveType.Circle)
                        Read("GetSegmentCircleCurve", () => { TopSolidHost.Sketches2D.GetSegmentCircleCurve(id, out var frame, out var radius); return new object[] { frame, radius }; });
                }
                Console.WriteLine("Read-only probe complete; no modification context opened.");
                return 0;
            }
        }
    }
}
