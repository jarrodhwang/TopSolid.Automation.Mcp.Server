using System;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ModelingGuideTools
    {
        public static void Register(Action<ToolDefinition> register) => register(new ToolDefinition("topsolid_get_modeling_guide",
            "Read local verified sketch/model/color/parameter workflows and explicit API limits. No TopSolid or network call. Consult before claiming a modeling capability is absent or substituting a different feature type.",
            new JObject { ["topic"] = Schema.Choice("One compact workflow; default features.", "features", "cylinder", "sketch2d", "sketch3d", "color", "parametric", "limitations") },
            p => Get((string)p["topic"] ?? "features"), "Design3D", api: ApiRefs.Kernel("IShapes", "IElements", "ISketches2D", "ISketches3D", "SmartReal")));
        internal static JObject Get(string topic)
        {
            var row = new JObject { ["topic"] = topic, ["basis"] = "TopSolid 7.20 Automation reference and matched installed SDK; registration is not live geometry verification." };
            row["names"] = "Omit geometry names unless the user requests them. TopSolid keeps its normal automatic names. Requested new names are made unique with numeric suffixes before execution; use the assigned names and returned handles in later requests.";
            row["sections"] = "Ordinary drawing and modeling creates no sections; the user never needs to say so. Only an explicit request to create a sketch section uses the separate confirmed create_sketch_section tool with existing closed profile handles.";
            switch (topic) {
                case "cylinder":
                    row["tool"] = "topsolid_create_cylinder";
                    row["workflow"] = "Get the intended document once. Pass diameter, height, optional method=extrude/revolve, base-center origin, axisDirection, units, color. Default absolute values, +Z from origin (0,0,0). The server creates a circle for extrude or one closed radial/axial rectangle for revolve, verifies volume and applies color in one confirmation. Never use extruded_rectangle for a cylinder.";
                    row["replacement"] = "For a user-requested replacement supply exact replaceShapes. The server validates the new cylinder before deleting those old shapes in the same transaction. Old source sketches remain. Never delete the working shape first."; break;
                case "sketch2d":
                    row["tool"] = "topsolid_create_sketches2d";
                    row["workflow"] = "Use profiles:[{kind:rectangle,origin:{x:0,y:0},width:50,height:350}] for a connected rectangular boundary, or one polyline with closed=true and no repeated first point. Four independent line entries do not form a native profile. Circle requires origin and radius, not center. Profiles are topology; no sketch sections are created.";
                    row["slot"] = "kind=slot uses center,length,width,rotationDegrees. length is overall end-to-end size > width. It creates two tangent lines and two analytic semicircles, a closed native profile. This draws a slot outline; it does not cut a pocket.";
                    row["placement"] = "Part: documentSpace=3d; profile coordinates are local sketch XY. placement=xy/xz/yz/frame/reference controls world placement. For XZ, local Y is world Z; for YZ, local Y is world Z. Reference placement remains associative only through the supported native anchor/plane/axis links."; break;
                case "sketch3d":
                    row["tool"] = "topsolid_create_sketch3d_curves";
                    row["workflow"] = "One new sketch, document absolute coordinates: line(start,end), polyline(points,closed), circle(center,normal,radius), arc(start,end,center,normal), bspline(controlPoints,periodic). Arc normal selects the right-hand sweep. Points use root units (default mm). Open paths remain open. B-spline control points are not interpolation points. Sketch geometry coordinates are literal; no inferred dimensional constraints."; break;
                case "color":
                    row["tool"] = "topsolid_set_entity_colors";
                    row["workflow"] = "Supply existing element handles and color:{r:255,g:0,b:0} for red. Supports native color-modifiable sketches, shapes, surfaces and other elements. It uses IsColorModifiable, SetColor and GetColor. No Color parameter is needed. inspect_elements returns colorModifiable.";
                    row["faces"] = "Face overrides are distinct from element color. Read exact face handles with topsolid_list_shape_faces; use topsolid_color_shape_faces. Never invent an ElementItemId label. Face colors can override the overall shape's color."; break;
                case "parametric":
                    row["default"] = "Use absolute values unless the user requests parameters/relations. A named parameter is a value provider, not a visual attribute.";
                    row["workflow"] = "Create or find named Real parameters once; create_parameter_expressions supports formulas. Feed their verified handles to lengthParameter (extrude), angleParameter (revolve), diameterParameter (through drilling), or heightParameter (extruded cylinder). Supply the literal OR the reference, never both. Length values in parameters are SI metres; angle parameters are SI radians. Native SmartReal(ElementId) preserves the dependency instead of copying its current value.";
                    row["limits"] = "Sketch vertex coordinates/circle radii are literal inputs in the verified sketch APIs. Driven sketch constraints and parameter-driven revolved-cylinder diameter/height are not implemented. Do not claim they follow a parameter or replace a requested dependency with a snapshot. The shape creation APIs do not expose generic feature-definition edit getters/setters; edit verified driving parameters, not guessed operation children."; break;
                case "limitations":
                    row["unsupported"] = new JArray("solid Boolean union/subtract/intersect", "boss/pocket on an existing body", "3D fillet/chamfer", "surface trim", "automatic sketch trim/fillet/chamfer constraints", "parameter-driven sketch dimensions");
                    row["reason"] = "No verified public creation/edit method for these operations was found in the bundled TopSolid 7.20 Automation corpus or the checked shape/sketch/part interfaces. They are not registered as executable tools. Do not invent API calls or claim a separate solid extrusion is a fused boss or cut pocket. Internal APIs require a separately verified implementation."; break;
                default:
                    row["available"] = new JArray("cylinder: extrude or revolve, optional color and atomic shape replacement", "closed-profile solid extrusion/revolution and loft", "surface extrusion/revolution/loft from supported native profiles", "through drilling on a Design part shape", "2D connected contours, circles, rectangles, slots, B-splines and computed curves", "3D lines/polylines/circles/arcs/B-splines", "element and face colors", "native parameter references for supported feature dimensions");
                    row["workflow"] = "Inspect context once, prepare the correct operation, and report verified receipts. Solid extrusion/revolution needs a closed native profile in a 3D document. Use original sketch handles; do not create sections or fabricate profile IDs. General extrusions create separate shapes with no Boolean fusion. Keep prior geometry until a replacement is validated.";
                    row["more"] = "Use topics cylinder/sketch2d/sketch3d/color/parametric/limitations for specific inputs and boundaries."; break;
            }
            return row;
        }
    }
}
