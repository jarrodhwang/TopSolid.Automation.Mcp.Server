using System;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject CreateShapeBatch(string operation, JObject p)
        {
            return Modify(p, operation + " sections", "kernel", (doc, current) =>
            {
                var rows = new JArray(); var scale = ModelingGeometry.Scale(current);
                foreach (JObject feature in current["features"])
                {
                    var section = Section(feature); var surface = (bool?)feature["surface"] ?? false; var centered = (bool?)feature["centered"] ?? false;
                    ElementId shape;
                    if (operation == "extrude") shape = TopSolidHost.Shapes.CreateExtrudedShape(doc, section,
                        new SmartDirection3D(SpatialInput.Direction(feature["direction"]), new Point3D(0, 0, 0)), new SmartReal(UnitType.Length, (double)feature["length"] * scale), null, centered, surface);
                    else
                    {
                        var degrees = (double)feature["angleDegrees"];
                        var origin = SpatialInput.Point(feature["axisOrigin"], scale); var direction = SpatialInput.Direction(feature["axisDirection"]);
                        shape = TopSolidHost.Shapes.CreateRevolvedShape(doc, section, new SmartAxis3D(new Axis3D(origin, direction), -1, 1),
                            degrees == 360 ? null : new SmartReal(UnitType.Angle, degrees * Math.PI / 180), centered, surface);
                    }
                    RequireValid(shape, "shape");
                    if (feature["name"] != null) TopSolidHost.Elements.SetName(shape, (string)feature["name"]);
                    var row = new JObject { ["inputIndex"] = rows.Count, ["shape"] = AutomationValues.Json(shape), ["surface"] = surface };
                    if (!surface)
                    {
                        var volume = TopSolidHost.Shapes.GetShapeVolume(shape);
                        if (!(volume > 0) || double.IsInfinity(volume)) throw new InvalidOperationException("Native solid volume is invalid; rolling back the batch.");
                        row["volumeCubicMetres"] = volume;
                    }
                    rows.Add(row);
                }
                return new JObject { ["items"] = rows, ["created"] = rows.Count };
            });
        }
    }
}
