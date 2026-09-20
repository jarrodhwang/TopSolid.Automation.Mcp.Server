using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Normalize the native organized CAM export. Use exporter-owned category roots, never user names.</summary>
internal static class CamContextPreview
{
    internal static string WorkerPath => Path.Combine(AppContext.BaseDirectory,"DracoPreview","TopSolid.DracoPreview.exe");
    internal static bool IsAvailable => File.Exists(WorkerPath);
    internal sealed record Scenes(PreviewScene Part, PreviewScene Work, PreviewScene MachinePart, PreviewScene Machine,
        PreviewScene? Original, PreviewScene? MachineOriginal, bool HasRemaining)
    {
        internal MachineVisibility? MachineElements { get; init; }
        internal PreviewScene? RemainingOnly { get; init; }
        internal PreviewScene? OriginalOnly { get; init; }
        internal PreviewScene Select(bool machine, CamStockMode stock, bool part)
        {
            if (part) return Select(machine, stock);
            var work = (stock == CamStockMode.Remaining ? RemainingOnly : stock == CamStockMode.Original ? OriginalOnly : null)
                ?? PreviewScene.Empty(Part.Bounds);
            return machine && MachineElements != null ? MachineElements.Compose(work) : work;
        }
        internal PreviewScene Select(bool machine, CamStockMode stock) => machine && MachineElements is { Hidden.Count: > 0 }
            ? MachineElements.Compose(Select(false, stock)) : stock == CamStockMode.Original && Original != null
            ? machine ? MachineOriginal! : Original
            : machine ? stock == CamStockMode.Remaining ? Machine : MachinePart : stock == CamStockMode.Remaining ? Work : Part;
    }

    internal static async Task<Scenes> ReadAsync(string path,CancellationToken token)
    {
        try { return await Task.Run(() => ReadCoreAsync(path, token), token).ConfigureAwait(false); }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidCastException or OverflowException or IndexOutOfRangeException or NullReferenceException or System.ComponentModel.Win32Exception)
        { throw new InvalidDataException("Invalid native CAM display data.", error); }
    }

    private static async Task<Scenes> ReadCoreAsync(string path,CancellationToken token)
    {
        if (!IsAvailable || new FileInfo(path).Length > GlbPreviewReader.MaximumBytes) throw new InvalidDataException("CAM context decoder unavailable or input exceeds its limit.");
        using var input=new BinaryReader(File.OpenRead(path));
        if(input.ReadUInt32()!=0x46546c67 || input.ReadUInt32()!=2 || input.ReadUInt32()!=input.BaseStream.Length) throw new InvalidDataException("Invalid CAM GLB.");
        var jsonLength=input.ReadInt32();
        if(jsonLength<2 || jsonLength>16*1024*1024 || input.ReadUInt32()!=0x4e4f534a) throw new InvalidDataException("Invalid CAM JSON.");
        JObject root;
        using(var reader=new JsonTextReader(new StringReader(Encoding.UTF8.GetString(input.ReadBytes(jsonLength)))) {MaxDepth=64})
            root=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
        var binaryLength=input.ReadInt32(); if(binaryLength<0 || input.ReadUInt32()!=0x004e4942 || binaryLength!=input.BaseStream.Length-input.BaseStream.Position) throw new InvalidDataException("Invalid CAM buffer.");
        var binaryStart=input.BaseStream.Position;
        if(root["buffers"] is not JArray {Count:1} buffers || buffers[0]["uri"]!=null) throw new InvalidDataException("External CAM buffers are not allowed.");
        var nodes=root["nodes"] as JArray ?? throw new InvalidDataException("Missing CAM nodes.");
        var meshes=root["meshes"] as JArray ?? throw new InvalidDataException("Missing CAM meshes.");
        if(nodes.Count>2048 || meshes.Count>2048) throw new InvalidDataException("CAM context exceeds its limit.");
        var sourceScene=(JObject)root["scenes"]![(int?)root["scene"]??0]!;
        var categories=(sourceScene["nodes"] as JArray)?.Values<int>().Select(i=>(JObject)nodes[i]).SingleOrDefault(n=>(string?)n["name"]=="EntityFilterTypes")
            ?? throw new InvalidDataException("No native CAM display categories.");
        var categoryIds=((JArray)categories["children"]!).Values<int>().ToArray();
        var machine=categoryIds.Where(i=>(string?)nodes[i]["name"]=="Machine").ToArray();
        var work=categoryIds.Where(i=>(string?)nodes[i]["name"] is "Environment" or "MachinedParts").ToArray();
        if(machine.Length!=1 || work.Length==0) throw new InvalidDataException("Missing native machine/work geometry groups.");
        // Organized TopSolid exports are Z-up, unlike the standard Y-up GLB exporter.
        // A design mesh may occur in several representations; select it only once via the CAM category.
        var designMeshes = new HashSet<int>();
        foreach (var representation in nodes.OfType<JObject>().Where(n => (string?)n["name"] == "Representations"))
            CollectMeshes(representation, designMeshes, new HashSet<int>(), 0);
        var machined = categoryIds.Where(i => (string?)nodes[i]["name"] == "MachinedParts").ToArray();
        var machinedMeshes = new HashSet<int>();
        foreach (var i in machined) CollectMeshes((JObject)nodes[i], machinedMeshes, new HashSet<int>(), 0);
        var stockMeshes = machinedMeshes.Where(i => !designMeshes.Contains(i)).ToHashSet();
        // Without a matching design representation the roles are ambiguous. Keep
        // native geometry visible, but never falsely label every work mesh as stock.
        if (!machinedMeshes.Overlaps(designMeshes)) stockMeshes.Clear();
        var original = categoryIds.Where(i => (string?)nodes[i]["name"] == "OriginalStock").ToArray();
        var originalMeshes = new HashSet<int>();
        foreach (var i in original) CollectMeshes((JObject)nodes[i], originalMeshes, new HashSet<int>(), 0);
        void CollectMeshes(JObject node, HashSet<int> result, HashSet<int> visitedNodes, int depth)
        {
            if (depth > 64) throw new InvalidDataException("Invalid CAM role hierarchy.");
            if (node["mesh"] is JValue m) result.Add((int)m);
            foreach (var id in node["children"] as JArray ?? [])
            {
                var i = (int)id;
                if (i < 0 || i >= nodes.Count || !visitedNodes.Add(i)) throw new InvalidDataException("Invalid CAM role reference.");
                CollectMeshes((JObject)nodes[i], result, visitedNodes, depth + 1);
            }
        }
        if (root["materials"] is not JArray) root["materials"] = new JArray();
        var materials = (JArray)root["materials"]!;
        var stockMaterial = materials.Count;
        materials.Add(new JObject { ["pbrMetallicRoughness"] = new JObject { ["baseColorFactor"] = new JArray(1d, 1d, 0d, .30), ["metallicFactor"] = 0, ["roughnessFactor"] = .9 }, ["alphaMode"] = "BLEND", ["doubleSided"] = true });
        foreach (var i in stockMeshes.Concat(originalMeshes))
            foreach (var primitive in ((JArray)meshes[i]["primitives"]!).OfType<JObject>()) primitive["material"] = stockMaterial;
        var usedMeshes=new HashSet<int>(); var visited=new HashSet<int>();
        foreach(var node in work.Concat(machine).Concat(original)) Visit(node,0);
        void Visit(int index,int depth)
        {
            token.ThrowIfCancellationRequested();
            if(depth>64 || index<0 || index>=nodes.Count || !visited.Add(index)) throw new InvalidDataException("Invalid CAM hierarchy.");
            if(nodes[index]["mesh"] is JValue m) usedMeshes.Add((int)m);
            foreach(var child in nodes[index]["children"] as JArray??[]) Visit((int)child,depth+1);
        }
        var primitives=usedMeshes.Order().SelectMany(i=>((JArray)meshes[i]["primitives"]!).OfType<JObject>()).Where(p=>((int?)p["mode"]??4)==4).ToArray();
        var directory=Path.Combine(Path.GetTempPath(),"TopSolid-Studio-draco-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var request=Path.Combine(directory,"request.bin");var decoded=Path.Combine(directory,"decoded.bin");
        try
        {
            using(var writer=new BinaryWriter(File.Create(request)))
            {
                writer.Write(0x44524331);writer.Write(primitives.Length);
                foreach(var primitive in primitives)
                {
                    var compression=primitive["extensions"]?["KHR_draco_mesh_compression"] as JObject??throw new InvalidDataException("Missing compressed CAM geometry.");
                    var view=root["bufferViews"]![(int)compression["bufferView"]!]!;
                    var offset=(long?)view["byteOffset"]??0;var length=(int)view["byteLength"]!;
                    if(offset<0 || length<=0 || length>64*1024*1024 || offset+length>binaryLength || (int?)view["buffer"]!=0) throw new InvalidDataException("Invalid compressed CAM range.");
                    writer.Write(length);writer.Write((int)compression["attributes"]!["POSITION"]!);writer.Write((int?)compression["attributes"]!["NORMAL"]??-1);
                    input.BaseStream.Position=binaryStart+offset;writer.Write(input.ReadBytes(length));
                }
            }
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var start=new ProcessStartInfo(WorkerPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};start.ArgumentList.Add(request);start.ArgumentList.Add(decoded);
            using(var process=Process.Start(start)??throw new IOException("Cannot start CAM decoder."))
            {
                var errors=process.StandardError.ReadToEndAsync(deadline.Token);
                try {await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);}
                catch (OperationCanceledException)
                {
                    try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { }
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    try { await errors.ConfigureAwait(false); } catch (OperationCanceledException) { }
                    token.ThrowIfCancellationRequested();
                    throw new IOException("CAM decoder exceeded its 45-second time limit.");
                }
                var message=await errors.ConfigureAwait(false);
                if(process.ExitCode!=0) throw new InvalidDataException("CAM decode failed: "+message);
            }
            return await Task.Run(()=>Build(),token).ConfigureAwait(false);
        }
        finally
        {
            try { foreach(var file in new[]{request,decoded}) if(File.Exists(file)) File.Delete(file);Directory.Delete(directory); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { App.DiagnosticLog.WriteException("preview.camContext.cleanup", error); }
        }

        Scenes Build()
        {
            using var reader=new BinaryReader(File.OpenRead(decoded));if(reader.ReadInt32()!=primitives.Length) throw new InvalidDataException("CAM decode count mismatch.");
            using var data=new MemoryStream();var views=new JArray();var accessors=new JArray();
            foreach(var primitive in primitives)
            {
                token.ThrowIfCancellationRequested();
                var points=reader.ReadInt32();var indices=reader.ReadInt32();var normals=reader.ReadInt32();
                if(points<=0 || points>6000000 || indices<=0 || indices>12000000 || normals is not (0 or 1)) throw new InvalidDataException("Invalid decoded CAM geometry.");
                var attributes=new JObject {["POSITION"]=Attribute(points,3,5126)};
                if(normals==1) attributes["NORMAL"]=Attribute(points,3,5126);
                primitive["attributes"]=attributes;primitive["indices"]=Attribute(indices,1,5125);primitive.Remove("extensions");primitive.Remove("extras");
            }
            if(reader.BaseStream.Position!=reader.BaseStream.Length) throw new InvalidDataException("Trailing decoded CAM data.");
            foreach(var mesh in meshes.OfType<JObject>()) mesh["primitives"]=new JArray(((JArray)mesh["primitives"]!).OfType<JObject>().Where(p=>primitives.Contains(p)).Select(p=>p.DeepClone()));
            root["accessors"]=accessors;root["bufferViews"]=views;root["buffers"]=new JArray(new JObject {["byteLength"]=data.Length});
            root.Remove("extensionsRequired");root.Remove("extensionsUsed");root.Remove("images");root.Remove("textures");root.Remove("samplers");
            foreach(var material in (root["materials"] as JArray??[]).OfType<JObject>())
            {material.Remove("extensions");material.Remove("normalTexture");material.Remove("occlusionTexture");material.Remove("emissiveTexture");(material["pbrMetallicRoughness"] as JObject)?.Remove("baseColorTexture");(material["pbrMetallicRoughness"] as JObject)?.Remove("metallicRoughnessTexture");}
            root["scene"]=0;
            var partScene = Scene(machined, stockMeshes);
            var stockScene = stockMeshes.Count == 0 ? null : Scene(machined, designMeshes).AsStockOverlay();
            var components = new Dictionary<int, PreviewScene>();
            var machineScene = Scene(machine.Concat(categoryIds.Where(i => (string?)nodes[i]["name"] == "Environment")).ToArray(), nodeScene: (i, scene) => components.Add(i, scene));
            var visibility = MachineVisibility.FromExport(nodes, machine[0], components);
            var originalScene = originalMeshes.Count == 0 ? null : Scene(original).AsStockOverlay();
            return new Scenes(partScene, PreviewScene.Combine(partScene, stockScene), PreviewScene.Combine(partScene, machineScene), PreviewScene.Combine(partScene, machineScene, stockScene),
                originalScene == null ? null : PreviewScene.Combine(partScene, originalScene),
                originalScene == null ? null : PreviewScene.Combine(partScene, machineScene, originalScene), stockScene != null)
                { MachineElements = visibility, RemainingOnly = stockScene, OriginalOnly = originalScene };
            int Attribute(int count,int components,int type)
            {
                var length=checked(count*components*4);if(data.Length+length>GlbPreviewReader.MaximumBytes) throw new InvalidDataException("Decoded CAM buffer exceeds limit.");
                var bytes=reader.ReadBytes(length);if(bytes.Length!=length) throw new InvalidDataException("Truncated CAM decode.");
                var view=views.Count;views.Add(new JObject {["buffer"]=0,["byteOffset"]=data.Length,["byteLength"]=length});data.Write(bytes);
                var accessor=accessors.Count;accessors.Add(new JObject {["bufferView"]=view,["componentType"]=type,["count"]=count,["type"]=components==1?"SCALAR":"VEC3"});return accessor;
            }
            PreviewScene Scene(int[] roots, HashSet<int>? excluded = null, Action<int, PreviewScene>? nodeScene = null)
            {
                var sceneRoot = (JObject)root.DeepClone();
                sceneRoot["scenes"]=new JArray(new JObject {["nodes"]=new JArray(roots)});
                foreach (var i in excluded ?? []) sceneRoot["meshes"]![i]!["primitives"] = new JArray();
                var json=Encoding.UTF8.GetBytes(sceneRoot.ToString(Formatting.None));var padded=(json.Length+3)&~3;
                using var output=new MemoryStream();using var writer=new BinaryWriter(output);
                writer.Write(0x46546c67);writer.Write(2);writer.Write(checked(28+padded+(int)data.Length));writer.Write(padded);writer.Write(0x4e4f534a);writer.Write(json);
                for(var i=json.Length;i<padded;i++)writer.Write((byte)32);writer.Write((int)data.Length);writer.Write(0x004e4942);data.Position=0;data.CopyTo(output);
                return GlbPreviewReader.Read(output.ToArray(),token,nativeColors:true,zUp:true,nodeScene:nodeScene);
            }
        }
    }
}

internal enum CamStockMode { Part, Remaining, Original }
