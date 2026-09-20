using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed partial class GraphicPreviewPane
{
    private readonly Border nativePathSurface = new() { Background = Brushes.White, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly Image nativePathImage = new() { Stretch = Stretch.Uniform };
    private JObject? nativePathOperation;
    private DispatcherTimer? nativePathRefresh;
    private long nativeCameraGeneration;
    private bool nativeRefreshRunning;
    internal bool HasNativeToolpathImage => nativePathSurface.Visibility == Visibility.Visible && nativePathImage.Source != null;

    private JObject NativeToolpathRequest(JObject operation, bool imageOnly)
    {
        var request = (JObject)operation.DeepClone();
        // WPF orthogonalizes its up vector; TopSolid expects that final camera basis.
        var look = Camera.LookDirection; look.Normalize();
        var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
        var up = Vector3D.CrossProduct(right, look); up.Normalize();
        request["view"] = new JObject
        {
            ["eye"] = new JArray(Camera.Position.X / 1000, Camera.Position.Y / 1000, Camera.Position.Z / 1000),
            ["look"] = new JArray(Camera.LookDirection.X, Camera.LookDirection.Y, Camera.LookDirection.Z),
            ["up"] = new JArray(up.X, up.Y, up.Z),
            ["angle"] = perspectiveMode ? perspectiveCamera.FieldOfView * Math.PI / 180 : 0,
            ["radius"] = perspectiveMode ? perspectiveDistance / 1000 : Camera.Width / 2000,
            ["machine"] = showMachine
        };
        if (imageOnly) request["nativeImage"] = true;
        return request;
    }
    private void PresentNativeToolpath(JObject result, JObject operation)
    {
        var bitmap = NativeToolpathImage.Read(result);
        nativePathOperation = (JObject)operation.DeepClone(); nativePathImage.Source = bitmap;
        nativePathSurface.Child = nativePathImage; nativePathSurface.Visibility = Visibility.Visible; nativePathSurface.Opacity = 1;
        // The native viewport may have a different aspect ratio; the local pixel scale is not valid over its image.
        compass.Visibility = Visibility.Collapsed;
        pathStatus.Text = StudioStrings.Get((bool?)result["upToDate"] == false ? "Preview.PathNativeOutdated" : "Preview.PathNative");
        pathStatus.Visibility = Visibility.Visible;
    }
    private void ClearNativeToolpath()
    {
        nativeCameraGeneration++; nativePathRefresh?.Stop(); nativePathOperation = null;
        nativePathImage.Source = null; nativePathSurface.Visibility = Visibility.Collapsed;
        if (Scene != null) compass.Visibility = Visibility.Visible;
    }
    private void QueueNativeToolpathRefresh()
    {
        if (disposed) return;
        nativeCameraGeneration++;
        if (nativePathOperation == null) return;
        nativePathSurface.Opacity = .55;
        if (nativePathRefresh == null)
        {
            nativePathRefresh = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(350) };
            nativePathRefresh.Tick += async (_, _) => await RefreshNativeToolpath();
        }
        nativePathRefresh.Stop(); nativePathRefresh.Start();
    }
    private async Task RefreshNativeToolpath()
    {
        nativePathRefresh?.Stop();
        if (nativeRefreshRunning || nativePathOperation == null || client is not IToolpathPreviewClient paths || disposed) return;
        var cameraVersion = nativeCameraGeneration; var current = generation; var operation = (JObject)nativePathOperation.DeepClone();
        nativeRefreshRunning = true;
        pathStatus.Text = StudioStrings.Get("Preview.PathLoading");
        try
        {
            var result = await paths.GetToolpathPreviewAsync(NativeToolpathRequest(operation, true), loading?.Token ?? CancellationToken.None);
            if (disposed || current != generation || cameraVersion != nativeCameraGeneration) return;
            if (!JToken.DeepEquals(result["operation"], operation) || (string?)result["status"] != "ready")
            { nativePathSurface.Visibility = Visibility.Collapsed; pathStatus.Text = StudioStrings.Get("Preview.PathUnavailable"); return; }
            PresentNativeToolpath(result, operation);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or Newtonsoft.Json.JsonException or FormatException or NotSupportedException)
        {
            App.DiagnosticLog.WriteException("preview.nativeToolpath", error);
            if (!disposed && current == generation && cameraVersion == nativeCameraGeneration)
            { nativePathSurface.Visibility = Visibility.Collapsed; pathStatus.Text = StudioStrings.Get("Preview.PathUnavailable"); }
        }
        finally
        {
            nativeRefreshRunning = false;
            if (!disposed && nativePathOperation != null && cameraVersion != nativeCameraGeneration) nativePathRefresh?.Start();
        }
    }
}

internal static class NativeToolpathImage
{
    internal static BitmapSource Read(JObject data)
    {
        var encoded = (string?)data["data"];
        if ((string?)data["format"] != "native-view-png" || (bool?)data["stateRestored"] != true ||
            encoded == null || encoded.Length > 11184812) throw new InvalidDataException("Invalid native toolpath image.");
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length < 33 || bytes.Length > 8 * 1024 * 1024 || !bytes.Take(8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }))
            throw new InvalidDataException("Invalid native toolpath PNG.");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if (width == 0 || height == 0 || width > 8192 || height > 8192 || (long)width * height > 16000000)
            throw new InvalidDataException("Native toolpath image is too large.");
        using var stream = new MemoryStream(bytes, false);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = decoder.Frames[0]; bitmap.Freeze(); return bitmap;
    }
}
