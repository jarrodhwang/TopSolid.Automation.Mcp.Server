using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Chat;

public enum PermissionMode { AskForApproval, ApproveForMe, FullAccess }

public sealed record PermissionDecision(bool RequiresApproval, string Reason);

/// <summary>Local policy applied only AFTER MCP prepares and validates an exact proposal.</summary>
public static class PermissionPolicy
{
    // Deliberately exact names: a newly installed or renamed tool needs review.
    // PDM creation is persistent and outside geometry undo, so it is not here.
    private static readonly HashSet<string> Reversible = new(StringComparer.Ordinal)
    {
        "topsolid_create_rectangle2d", "topsolid_create_circle2d", "topsolid_create_polyline3d",
        "topsolid_create_sketch3d_curves", "topsolid_create_extruded_rectangle", "topsolid_create_cylinder",
        "topsolid_create_sketches2d", "topsolid_create_heart_sketch", "topsolid_create_sketch_profiles",
        "topsolid_create_sketch_section", "topsolid_create_contour2d", "topsolid_append_sketch_contour",
        "topsolid_create_points2d", "topsolid_create_points3d", "topsolid_update_points2d", "topsolid_update_points3d",
        "topsolid_extrude_sections", "topsolid_revolve_sections", "topsolid_extrude_sketch", "topsolid_revolve_sketch", "topsolid_loft_sketch",
        "topsolid_set_entity_colors", "topsolid_color_shape_faces", "topsolid_set_element_visibility",
        "topsolid_rename_element", "topsolid_translate_element", "topsolid_translate_elements",
        "topsolid_set_sketch_item_fixed", "topsolid_set_sketch2d_items_fixed",
        "topsolid_create_entity_folders", "topsolid_move_entities", "topsolid_update_elements",
        "topsolid_create_parameters", "topsolid_create_parameter_expressions",
        "topsolid_set_parameter_value", "topsolid_set_parameter_values", "topsolid_set_parameter_expressions"
    };
    private static readonly HashSet<string> ReviewedAdditional = new(StringComparer.Ordinal)
    {
        "topsolid_set_document_universal_id", "topsolid_update_pdm_objects", "topsolid_delete_pdm_documents",
        "topsolid_restore_pdm_documents", "topsolid_check_in_pdm_objects", "topsolid_save_documents", "topsolid_save_document",
        "topsolid_delete_elements", "topsolid_delete_sketch2d_items", "topsolid_open_document",
        "topsolid_update_document", "topsolid_rebuild_document", "topsolid_rename_document",
        "topsolid_create_project", "topsolid_create_folder", "topsolid_create_document", "topsolid_create_part_document",
        "topsolid_create_through_drilling", "topsolid_include_assembly_document", "topsolid_translate_assembly_inclusion",
        "topsolid_execute_cam_operation", "topsolid_set_cam_parameter_value",
        "topsolid_generate_nc_for_selection", "topsolid_export_nc_file"
    };

    public static string Label(PermissionMode mode) => StudioStrings.Text(mode switch
    {
        PermissionMode.ApproveForMe => "Approve for me", PermissionMode.FullAccess => "Full access", _ => "Ask for approval"
    });

    public static string Description(PermissionMode mode) => StudioStrings.Text(mode switch
    {
        PermissionMode.ApproveForMe => "Automatically approve known reversible CAD edits. Ask before persistent PDM changes, saving, deletion, replacement, CAM execution, and unknown actions.",
        PermissionMode.FullAccess => "Automatically approve known MCP actions, including persistent and destructive changes. MCP preparation, exact target validation, and cancellation still apply. Unknown actions require approval.",
        _ => "Review the exact target and arguments before each TopSolid change. Read-only queries run directly."
    });

    public static PermissionDecision Evaluate(PermissionMode mode, JObject proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal["toolName"]?.Type != JTokenType.String || proposal["arguments"] is not JObject arguments ||
            proposal["target"] is not JObject { Count: > 0 })
            return new(true, "The MCP proposal is incomplete and requires review.");
        var tool = (string)proposal["toolName"]!;
        if (mode == PermissionMode.AskForApproval || !Enum.IsDefined(mode))
            return new(true, "Ask for approval is selected.");
        if (!Reversible.Contains(tool) && !ReviewedAdditional.Contains(tool))
            return new(true, "This action is not in the reviewed local permission policy.");
        if (mode == PermissionMode.FullAccess)
            return new(false, "Full access approved this known, prepared MCP action.");
        if (!Reversible.Contains(tool) || HasReplacementOrPersistence(arguments))
            return new(true, "This action can replace, delete, persist, or otherwise affect data beyond the automatic CAD edit policy.");
        return new(false, "Approve for me approved this known reversible CAD edit.");
    }

    private static bool HasReplacementOrPersistence(JToken token)
    {
        // Inspect nested batches too. Preview prose/model text never grants authority.
        if (token is JObject obj)
            foreach (var property in obj.Properties())
            {
                var name = property.Name.ToLowerInvariant();
                var sensitive = name.Contains("replace", StringComparison.Ordinal) || name.Contains("delete", StringComparison.Ordinal) ||
                    name.Contains("save", StringComparison.Ordinal) || name.Contains("checkin", StringComparison.Ordinal) ||
                    name.Contains("export", StringComparison.Ordinal) || name.Contains("overwrite", StringComparison.Ordinal);
                if (sensitive && property.Value.Type != JTokenType.Null &&
                    !(property.Value.Type == JTokenType.Boolean && !(bool)property.Value) &&
                    !(property.Value is JArray { Count: 0 })) return true;
                if (HasReplacementOrPersistence(property.Value)) return true;
            }
        else if (token is JArray array && array.Any(HasReplacementOrPersistence)) return true;
        return false;
    }
}
