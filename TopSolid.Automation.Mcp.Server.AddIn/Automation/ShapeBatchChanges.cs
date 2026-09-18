using System;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject CreateShapeBatch(string operation, JObject p)
        {
            return Modify(p, operation + " sketch features", "kernel", (doc, current) =>
            {
                var rows = new JArray(); var scale = ModelingGeometry.Scale(current);
                foreach (JObject feature in current["features"])
                {
                    var section = Section(feature); var surface = (bool?)feature["surface"] ?? false; var centered = (bool?)feature["centered"] ?? false;
                    ElementId shape; var dimension = operation == "extrude" ? FeatureDimension.Length(feature, "length", scale) : FeatureDimension.Angle(feature);
                    if (operation == "extrude") shape = TopSolidHost.Shapes.CreateExtrudedShape(doc, section,
                        new SmartDirection3D(SpatialInput.Direction(feature["direction"]), new Point3D(0, 0, 0)), dimension.Smart, null, centered, surface);
                    else
                    {
                        var origin = SpatialInput.Point(feature["axisOrigin"], scale); var direction = SpatialInput.Direction(feature["axisDirection"]);
                        shape = TopSolidHost.Shapes.CreateRevolvedShape(doc, section, new SmartAxis3D(new Axis3D(origin, direction), -1, 1),
                            dimension.RevolutionAngle, centered, surface);
                    }
                    RequireValid(shape, "shape");
                    CreationNames.SetAndVerify(TopSolidHost.Elements, shape, (string)feature["name"]);
                    var row = new JObject { ["inputIndex"] = rows.Count, ["shape"] = AutomationValues.Json(shape), ["surface"] = surface };
                    row["name"] = TopSolidHost.Elements.GetName(shape); row["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(shape);
                    row["dimension"] = dimension.Receipt;
                    if (feature["color"] != null) row["color"] = ElementAppearance.Set(TopSolidHost.Elements, shape, feature["color"]);
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
