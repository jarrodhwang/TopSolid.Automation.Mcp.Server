using System.Xml;
using System.Xml.Linq;
using TopSolid.Automation.AI.Studio.Appearance;

namespace TopSolid.Automation.Tests;

internal static class ThemeTests
{
    public static Task Run()
    {
        var classic = XDocument.Parse("""
            <ConfigData><Folders><Folder name="TopSolid"><Folder name="Kernel">
              <Folder name="WX"><Folder name="StartPage"><Folder name="StartTabPage">
                <Value name="ColorTheme" type="Int">2</Value>
              </Folder></Folder><Folder name="Application"><Value name="IsThemeMigrated">True</Value></Folder></Folder>
            </Folder></Folder></Folders></ConfigData>
            """);
        Assert(TopSolidThemeFollower.ReadThemeName(classic) == "TopSolid Classic", "A start-page color choice must not enable application dark mode.");
        var application = classic.Descendants("Folder").Single(folder => (string?)folder.Attribute("name") == "Application");
        application.Add(new XElement("Value", new XAttribute("name", "CurrentTheme"), "TopSolid Dark"));
        Assert(TopSolidThemeFollower.ReadThemeName(classic) == "TopSolid Dark", "The persisted TopSolid theme selection must be honored.");

        var customDark = TopSolidThemeFollower.ReadUserTheme(XDocument.Parse("""
            <Theme name="Workshop" enableLowBrightness="False" version="7.20.0.0"><Colors>
              <Color name="global_dialog_back">41,41,41</Color><Color name="global_dialog_text">250,250,250</Color>
              <Color name="global_title_top">10,10,10</Color><Color name="invalid">999,0,0</Color>
              <Color name="incomplete">10,20</Color>
            </Colors></Theme>
            """), "Workshop", "fixture");
        Assert(customDark.IsDark, "Custom UI brightness must be derived from the dialog color.");
        Assert(customDark.Colors?["global_dialog_back"] == "#292929", "Valid RGB values must be preserved exactly.");
        Assert(customDark.Colors?.ContainsKey("invalid") == false && customDark.Colors?.ContainsKey("incomplete") == false,
            "Malformed custom color values must not reach brush conversion.");

        var entityInversion = TopSolidThemeFollower.ReadUserTheme(XDocument.Parse("""
            <Theme name="Workshop" enableLowBrightness="True"><Colors>
              <Color name="global_dialog_back">246,246,250</Color>
            </Colors></Theme>
            """), "Workshop", "fixture");
        Assert(!entityInversion.IsDark, "Low brightness inverts CAD entities and must not override a light UI background.");
        try
        {
            TopSolidThemeFollower.ReadUserTheme(XDocument.Parse("<Other/>"), "fixture", "fixture");
            throw new InvalidOperationException("An unrelated XML document was accepted as a TopSolid theme.");
        }
        catch (XmlException) { }
        TestModes(customDark);
        TestSavedThemes();
        return Task.CompletedTask;
    }

    private static void TestModes(TopSolidThemeSnapshot customDark)
    {
        static TopSolidThemeSnapshot UnselectedTopSolid() => throw new InvalidOperationException("An unselected TopSolid source was read.");
        static bool UnselectedSystem() => throw new InvalidOperationException("An unselected Windows source was read.");
        Assert(!TopSolidThemeFollower.ResolveMode(" LIGHT ", UnselectedTopSolid, UnselectedSystem).IsDark,
            "Light must remain light independently of Windows and TopSolid.");
        Assert(TopSolidThemeFollower.ResolveMode("Dark", UnselectedTopSolid, UnselectedSystem).IsDark,
            "Dark must remain dark independently of Windows and TopSolid.");
        Assert(TopSolidThemeFollower.ResolveMode("system", UnselectedTopSolid, () => false).IsDark,
            "System must follow the Windows app preference.");
        Assert(!TopSolidThemeFollower.ResolveMode("system", UnselectedTopSolid, () => true).IsDark,
            "A later Windows light preference must resolve without changing the selected mode.");
        foreach (var mode in new string?[] { "topsolid", "unknown", null })
            Assert(ReferenceEquals(customDark, TopSolidThemeFollower.ResolveMode(mode, () => customDark, UnselectedSystem)),
                "TopSolid and the default mode must retain the selected custom palette and source.");
    }

    private static void TestSavedThemes()
    {
        var root = Path.Combine(Path.GetTempPath(), "TopSolid-theme-fixture-" + Guid.NewGuid().ToString("N"));
        var version = Path.Combine(root, "7.20");
        var configPath = Path.Combine(version, "TopSolid", "Kernel", "SX", "Config.ConfigData.xml");
        var themes = Path.Combine(version, "Themes");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        Directory.CreateDirectory(themes);
        try
        {
            WriteSelection("TopSolid Dark");
            Assert(TopSolidThemeFollower.ReadCurrent(root).IsDark, "Saved application dark selection must be read.");
            WriteSelection("TopSolid Classic");
            Assert(!TopSolidThemeFollower.ReadCurrent(root).IsDark, "Later saved Classic selection must be followed.");
            var customPath = Path.Combine(themes, "Workshop.xml");
            File.WriteAllText(customPath, "<Theme><Colors><Color name=\"global_dialog_back\">41,41,41</Color></Colors></Theme>");
            WriteSelection("Workshop");
            var custom = TopSolidThemeFollower.ReadCurrent(root);
            Assert(custom.IsDark && custom.Name == "Workshop" && custom.Source == customPath,
                "A saved custom theme must preserve identity, brightness and provenance.");
            File.WriteAllText(customPath, "<Theme><Colors><Color name=\"global_dialog_back\">246,246,250</Color></Colors></Theme>");
            var changed = TopSolidThemeFollower.ReadCurrent(root);
            Assert(!changed.IsDark && changed.Colors?["global_dialog_back"] == "#F6F6FA",
                "Saved custom color changes must be read even when the selected theme name is unchanged.");
            WriteSelection("../outside");
            Assert(TopSolidThemeFollower.ReadCurrent(root).Name == "TopSolid Classic",
                "Selected custom theme names must not traverse out of the theme directory.");
        }
        finally { Directory.Delete(root, recursive: true); }

        void WriteSelection(string selected)
        {
            new XDocument(new XElement("ConfigData", new XElement("Folder", new XAttribute("name", "Application"),
                new XElement("Value", new XAttribute("name", "CurrentTheme"), selected)))).Save(configPath);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
