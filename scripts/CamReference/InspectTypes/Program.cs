using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Usage: InspectTypes <TopSolid bin> <output.json>");
var rows = new List<object>();
// Read PE metadata only: no Assembly.Load, code execution or TopSolid connection.
foreach (var file in Directory.EnumerateFiles(args[0], "TopSolid.Cam.NC*.dll").Order())
{
    using var stream = File.OpenRead(file);
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata) continue;
    var reader = pe.GetMetadataReader();
    var assembly = reader.GetAssemblyDefinition();
    var types = new List<object>();
    foreach (var handle in reader.TypeDefinitions)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        var ns = reader.GetString(type.Namespace);
        if (!name.EndsWith("Operation", StringComparison.Ordinal) || !ns.StartsWith("TopSolid.Cam.NC.", StringComparison.Ordinal) || !ns.Contains(".DB")) continue;
        if (type.Attributes.HasFlag(TypeAttributes.Interface)) continue;
        string? baseType = null;
        if (type.BaseType.Kind == HandleKind.TypeReference)
        {
            var reference = reader.GetTypeReference((TypeReferenceHandle)type.BaseType);
            baseType = reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);
        }
        else if (type.BaseType.Kind == HandleKind.TypeDefinition)
        {
            var definition = reader.GetTypeDefinition((TypeDefinitionHandle)type.BaseType);
            baseType = reader.GetString(definition.Namespace) + "." + reader.GetString(definition.Name);
        }
        types.Add(new { nativeType = ns + "." + name, isAbstract = type.Attributes.HasFlag(TypeAttributes.Abstract),
            isPublic = (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public, baseType });
    }
    if (types.Count == 0) continue;
    rows.Add(new { assembly = reader.GetString(assembly.Name), version = assembly.Version.ToString(),
        sourcePath = Path.GetFullPath(file), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), types });
}
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Read operation metadata from {rows.Count} assemblies; no TopSolid code was loaded.");
