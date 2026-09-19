using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CylinderTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var p = DocumentActionTools.Target();
            p["diameter"] = Schema.Number("Cylinder diameter, not radius, in input units.", .001, 100000);
            p["height"] = Schema.Number("Absolute cylinder height in input units. Use this by default.", .001, 100000);
            p["heightParameter"] = Schema.Element(); p["heightParameter"]["description"] = "Existing Real Length parameter; alternative to height, only for method=extrude. Preserves a native dependency when requested.";
            p["method"] = Schema.Choice("Native operation: extrude (default) or revolve. Revolve builds a closed radial/axial rectangle and revolves it 360 degrees.", "extrude", "revolve");
            p["origin"] = ShapeWorkflowTools.Vector("Base center in world input units; default zero");
            p["axisDirection"] = ShapeWorkflowTools.Vector("Direction from base to top; default +Z; normalized");
            p["units"] = Schema.Choice("Input lengths; default mm.", "mm", "cm", "m"); p["name"] = Schema.Text(CreationNames.Description + " Applies to the shape; its source sketch keeps an automatic name.", 100);
            p["color"] = AppearanceTools.ColorSchema(); p["replaceShapes"] = Schema.Array(Schema.Element(), 1, 16);
            p["replaceShapes"]["description"] = "Only when user requests replacement: exact old shape handles to remove after successful new-cylinder validation, in the same transaction. Old source sketches remain. Omit for ordinary creation.";
            register(new ToolDefinition("topsolid_create_cylinder", "Create a round cylinder by extrusion or revolution, optionally color it, in ONE confirmed transaction. Builds the correct closed native profile without sections and verifies cylinder volume. Prefer this to multiple sketch/model/color calls. Absolute dimensions by default; optional extruded height parameter. Does not save.",
                p, a.CreateCylinder, "Design3D", new[] { "documentId", "diameter" }, false,
                ApiRefs.Kernel("IShapes.CreateExtrudedShape", "IShapes.CreateRevolvedShape", "IShapes.GetShapeVolume", "IShapes.GetShapes", "IElements.IsDeletable", "IElements.DeleteSeveral", "IElements.SetName", "IElements.Exists", "IElements.IsInvalid",
                    "ISketches2D.CreateSketchIn3D", "ISketches2D.CreateVertex", "ISketches2D.CreateCircleSegment", "ISketches2D.CreateLineSegment", "ISketches2D.CreateProfile", "ISketches2D.StartModification", "ISketches2D.EndModification", "ISketches2D.CreateBuildingOperation", "ISketches2D.GetSectionCount", "SmartSection3D.-ctor")
                    .Concat(ShapeWorkflowTools.DimensionApi).Concat(SketchPlanTools.Api).Concat(AppearanceTools.Api).Distinct().ToArray(),
                p2 => { MutationReferences.Validate(p2); ElementAppearance.Validate(p2); FeatureDimension.Validate(p2, "height", "heightParameter"); CylinderPlan.Parse(p2, p2["height"] == null ? 1 : (double)p2["height"] * ModelingGeometry.Scale(p2)); }, a.PreviewCylinder,
                "Create a cylinder and optional color; remove only reviewed replacement shapes after validating the new result. Roll back all document changes on failure. Never delete source sketches or save automatically.", defaultLengthUnits: "mm", minimumTopSolidVersion: TopSolidVersionSupport.ModelingMinimumVersion));
        }
    }
}
