using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void SketchPrimitives()
        {
            var star = JObject.Parse("{kind:'star',center:{x:12,y:-5},outerRadius:50,innerRadius:20,pointCount:5,rotationDegrees:90}");
            var plan = SketchCurveGeometry.Parse(star, .001);
            Check(plan.Closed && plan.Points.Length == 10 && plan.SegmentCount == 10, "Star must have exact closed shared topology");
            Check(SketchCurveGeometry.Distance(plan.Points[0], new Point2D(.012, .045)) < 1e-12, "Star placement or first tip orientation differs");
            for (var i = 0; i < plan.Points.Length; i++)
                Check(Math.Abs(SketchCurveGeometry.Distance(plan.Points[i], plan.Center) - (i % 2 == 0 ? .05 : .02)) < 1e-12, "Star radii lost accuracy");
            star["innerRadius"] = 51; Throws<ArgumentException>(() => SketchCurveGeometry.Parse(star, .001));
            var ellipse = JObject.Parse("{kind:'ellipse',center:{x:12,y:-5},majorRadius:60,minorRadius:40,rotationDegrees:0,tolerance:0.01}");
            foreach (var angle in new[] { 0.0, 90.0, -35.0 })
            foreach (var scale in new[] { .001, .01, 1.0 })
            {
                ellipse["rotationDegrees"] = angle;
                var curve = SketchCurveGeometry.Parse(ellipse, scale);
                Check(curve.Closed && curve.Points.Length == 3 * curve.SegmentCount && curve.SegmentCount % 4 == 0, "Ellipse must share endpoints and retain cardinal extrema");
                Check(curve.ApproximationBound <= .01 * scale, "Ellipse planned bound exceeds requested tolerance");
                var radians = angle * Math.PI / 180;
                for (var segment = 0; segment < curve.SegmentCount; segment++)
                for (var j = 0; j <= 25; j++)
                {
                    var theta = 2 * Math.PI * (segment + j / 25.0) / curve.SegmentCount;
                    var x = 60 * Math.Cos(theta); var y = 40 * Math.Sin(theta);
                    var expected = new Point2D((12 + x * Math.Cos(radians) - y * Math.Sin(radians)) * scale, (-5 + x * Math.Sin(radians) + y * Math.Cos(radians)) * scale);
                    var actual = SketchCurveGeometry.Cubic(curve.CubicSegment(segment), j / 25.0);
                    Check(SketchCurveGeometry.Distance(actual, expected) <= curve.ApproximationBound + 1e-12, "Ellipse fails its analytic deviation bound");
                }
                Check(SketchCurveGeometry.Distance(curve.CubicSegment(curve.SegmentCount - 1)[3], curve.Points[0]) == 0, "Ellipse has an open seam");
            }
            ellipse["minorRadius"] = 61; Throws<ArgumentException>(() => SketchCurveGeometry.Parse(ellipse, .001)); ellipse["minorRadius"] = 40;
            ellipse["tolerance"] = 1e-12; Throws<ArgumentException>(() => SketchCurveGeometry.Parse(ellipse, .001));
            var batch = JObject.Parse("{documentId:'rev',documentSpace:'3d',units:'mm',sketches:[{name:'Tree',placement:'xy',units:'mm',profiles:[{kind:'rectangle',origin:{x:0,y:0},width:10,height:25,units:'mm'}]}]}");
            var snapshot = batch.DeepClone();
            using (var gateway = new AutomationGateway())
            {
                var schema = (JObject)new ToolRegistry(gateway).List().Single(t => (string)t["name"] == "topsolid_create_sketches2d")["inputSchema"];
                Schema.Validate(batch, schema); SketchPlanTools.Validate(batch); checks++;
                Check(JToken.DeepEquals(snapshot, batch), "Compatible unit handling mutated the proposed arguments");
                var malformed = (JObject)batch.DeepClone(); ((JObject)malformed["sketches"][0]).Remove("profiles"); malformed["sketches"][0]["kind"] = "parabola";
                var nesting = Throws<RpcException>(() => Schema.Validate(malformed, schema));
                Check(nesting.Message.Contains("sketches[0].profiles") && nesting.Message.Contains("inside profiles"), "Malformed sketch must identify its index and geometry nesting in one error");
                batch["sketches"][0]["units"] = "m"; Throws<ArgumentException>(() => SketchPlanTools.Validate(batch));
                batch["sketches"][0]["units"] = "mm"; batch["sketches"][0]["profiles"][0]["units"] = "cm";
                Throws<ArgumentException>(() => SketchPlanTools.Validate(batch));
                batch["units"] = "cm"; batch["sketches"][0]["units"] = "cm"; SketchPlanTools.Validate(batch); checks++;
                batch.Remove("units"); Throws<ArgumentException>(() => SketchPlanTools.Validate(batch));
            }
        }
    }
}
