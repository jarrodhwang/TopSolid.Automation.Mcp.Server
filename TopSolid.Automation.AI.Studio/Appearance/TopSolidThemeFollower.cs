using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>Read-only appearance selection and following. Never loads or calls vendor assemblies.</summary>
public sealed class TopSolidThemeFollower : IDisposable
{
    private readonly DispatcherTimer timer;
    private bool disposed;
    private bool reading;
    private string? lastError;
    private string mode;
    private int selectionVersion;
    public string Mode => mode;
    public TopSolidThemeSnapshot Current => TopSolidTheme.Current;
    public event EventHandler? Changed;

    public TopSolidThemeFollower(Window owner, string mode = "topsolid")
    {
        this.mode = NormalizeMode(mode);
        // Resolve before first paint; subsequent reads run outside the UI thread.
        try { TopSolidTheme.Apply(ReadMode(this.mode)); }
        catch (Exception ex) when (IsReadFailure(ex)) { TopSolidTheme.Apply(Current); ReportFailure(ex); }
        TopSolidTheme.ApplyWindow(owner);
        timer = new DispatcherTimer(DispatcherPriority.Background, owner.Dispatcher) { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += Tick;
        timer.Start();
    }

    public void Refresh() => Tick(this, EventArgs.Empty);

    public void SetMode(string mode)
    {
        timer.Dispatcher.VerifyAccess();
        if (disposed) return;
        this.mode = NormalizeMode(mode);
        selectionVersion++;
        try { ApplyIfChanged(ReadMode(this.mode)); }
        catch (Exception ex) when (IsReadFailure(ex)) { ReportFailure(ex); }
    }

    private async void Tick(object? sender, EventArgs e)
    {
        if (disposed || reading || mode is "light" or "dark") return;
        reading = true;
        var requestedMode = mode;
        var requestedVersion = selectionVersion;
        try
        {
            var next = await Task.Run(() => ReadMode(requestedMode));
            // A pending TopSolid/system read cannot overwrite a newer explicit selection.
            if (disposed || requestedVersion != selectionVersion) return;
            ApplyIfChanged(next);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            if (!disposed && requestedVersion == selectionVersion) ReportFailure(ex);
        }
        finally { reading = false; }
    }

    private void ApplyIfChanged(TopSolidThemeSnapshot next)
    {
        lastError = null;
        if (next.Signature == Current.Signature) return;
        TopSolidTheme.Apply(next);
        Changed?.Invoke(this, EventArgs.Empty);
        App.DiagnosticLog.Write("info", "appearance.themeChanged", next.Name + " · " + next.Source);
    }

    public static string NormalizeMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "light" => "light", "dark" => "dark", "system" => "system", _ => "topsolid"
    };

    /// <summary>Resolve without touching either source unless that source is selected.</summary>
    public static TopSolidThemeSnapshot ResolveMode(string? mode, Func<TopSolidThemeSnapshot> readTopSolid, Func<bool> systemUsesLightTheme)
    {
        return NormalizeMode(mode) switch
        {
            "light" => new(false, "Light", "Studio appearance"),
            "dark" => new(true, "Dark", "Studio appearance"),
            "system" => new(!systemUsesLightTheme(), "System", "Windows app appearance"),
            _ => readTopSolid()
        };
    }

    private static TopSolidThemeSnapshot ReadMode(string mode) => ResolveMode(mode, ReadCurrent, ReadSystemUsesLightTheme);

    private static bool ReadSystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
        // An absent Windows app preference uses the standard light appearance.
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private static bool IsReadFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException or System.Security.SecurityException;
    private void ReportFailure(Exception ex)
    {
        // TopSolid may be replacing its config. Retain the last valid palette and retry next tick.
        var reason = ex.GetType().Name + ": " + ex.Message;
        if (reason == lastError) return;
        lastError = reason;
        App.DiagnosticLog.Write("warning", "appearance.themeRead", reason);
    }

    public static TopSolidThemeSnapshot ReadCurrent()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TOPSOLID", "TopSolid");
        return ReadCurrent(root);
    }

    public static TopSolidThemeSnapshot ReadCurrent(string root)
    {
        if (!Directory.Exists(root)) return new(false, "TopSolid Classic", "TopSolid settings unavailable; Classic fallback");
        var directories = Directory.EnumerateDirectories(root)
            .Select(path => new { Path = path, Version = Version.TryParse(Path.GetFileName(path), out var v) ? v : null })
            .Where(item => item.Version != null).OrderByDescending(item => item.Version);
        foreach (var directory in directories)
        {
            var config = Path.Combine(directory.Path, "TopSolid", "Kernel", "SX", "Config.ConfigData.xml");
            if (!File.Exists(config)) continue;
            var document = ReadXml(config);
            var selected = ReadThemeName(document);
            if (selected is "TopSolid Classic" or "TopSolid Dark")
                return new(selected == "TopSolid Dark", selected, config);
            // A selected user theme is a file name, never an arbitrary path from configuration.
            if (selected.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || selected is "." or "..")
                return new(false, "TopSolid Classic", "Invalid TopSolid theme name; Classic fallback");
            var customPath = Path.Combine(directory.Path, "Themes", selected + ".xml");
            if (!File.Exists(customPath)) return new(false, "TopSolid Classic", "Selected TopSolid theme unavailable; Classic fallback");
            return ReadUserTheme(ReadXml(customPath), selected, customPath);
        }
        return new(false, "TopSolid Classic", "TopSolid settings unavailable; Classic fallback");
    }

    public static string ReadThemeName(XDocument config)
    {
        // ColorTheme under StartPage is unrelated. Only CurrentTheme selects application appearance.
        return config.Descendants("Folder").Where(folder => (string?)folder.Attribute("name") == "Application")
            .SelectMany(folder => folder.Elements("Value"))
            .FirstOrDefault(value => (string?)value.Attribute("name") == "CurrentTheme")?.Value.Trim() is { Length: > 0 } selected
            ? selected : "TopSolid Classic";
    }

    public static TopSolidThemeSnapshot ReadUserTheme(XDocument document, string name, string source)
    {
        if (document.Root?.Name.LocalName != "Theme") throw new XmlException("Invalid TopSolid theme document.");
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in document.Root.Element("Colors")?.Elements("Color") ?? [])
        {
            var key = (string?)item.Attribute("name");
            var parts = item.Value.Split(',');
            if (key == null || parts.Length != 3 || !byte.TryParse(parts[0], out var r) ||
                !byte.TryParse(parts[1], out var g) || !byte.TryParse(parts[2], out var b)) continue;
            colors[key] = $"#{r:X2}{g:X2}{b:X2}";
        }
        // Low brightness in TopSolid inverts CAD entities; UI brightness comes from the dialog background.
        var dark = colors.TryGetValue("global_dialog_back", out var background)
            && Convert.ToInt32(background.Substring(1, 2), 16) * .2126
             + Convert.ToInt32(background.Substring(3, 2), 16) * .7152
             + Convert.ToInt32(background.Substring(5, 2), 16) * .0722 < 128;
        return new(dark, name, source, colors);
    }

    private static XDocument ReadXml(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 8 * 1024 * 1024) throw new IOException("TopSolid appearance settings exceed the read limit.");
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024
        });
        return XDocument.Load(reader);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= Tick;
        Changed = null;
    }
}
