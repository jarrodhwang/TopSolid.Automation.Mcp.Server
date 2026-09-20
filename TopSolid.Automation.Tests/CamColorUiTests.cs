using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.Tests;

internal static partial class UiShellTests
{
    internal static bool CamColorsOnly;
    private static async Task VerifyCamColorsUi(string output)
    {
        var isolated=Path.Combine(Path.GetTempPath(),"Studio-color-ui-"+Guid.NewGuid().ToString("N"));
        var store=new SettingsStore(isolated);
        var window=new MainWindow(false,settingsOverride:store) {Width=1100,Height=780,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
        try
        {
            window.Show(); (Field(window,"themeFollower") as IDisposable)?.Dispose();
            var add=Control<Button>(window,"AttachButton"); var permission=Control<ComboBox>(window,"PermissionBox");
            Check.Equal(3,add.ContextMenu.Items.Count,"Add menu must contain Files/CAD/CAM only");
            foreach(var dark in new[]{false,true})
            {
                TopSolidTheme.Apply(new(dark,dark?"Dark":"Classic","CAM colors UI fixture"));
                foreach(var index in new[]{1,2}) ((MenuItem)add.ContextMenu.Items[index]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var options=store.Load().ContextOptions;
                if(!options.Cad) ((MenuItem)add.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if(!options.Cam) ((MenuItem)add.ContextMenu.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                await Layout(window);
                Check.True(Control<Button>(window,"CadModeButton").IsVisible && Control<Button>(window,"CamModeButton").IsVisible,"Mode badges missing");
                var before=permission.SelectedIndex;
                Control<Button>(window,"ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check.Equal(before,permission.SelectedIndex,"Mode changed Access policy");
                Check.True(store.Load().ContextOptions.Cad && store.Load().ContextOptions.Cam,"Chat clear reset modes");
                var setBusy=typeof(MainWindow).GetMethod("SetBusy",BindingFlags.Instance|BindingFlags.NonPublic)!;
                setBusy.Invoke(window,[true]);Check.True(!Control<Button>(window,"CamModeButton").IsEnabled&&!add.IsEnabled,"Mode changed during request");setBusy.Invoke(window,[false]);
                window.Width=1100;await Layout(window);Render(window,Path.Combine(output,$"cam-colors-composer-{(dark?"dark":"light")}.png"));
                window.Width=window.MinWidth;await Layout(window);
                var model=Control<ComboBox>(window,"ModelBox");var badge=Control<Button>(window,"CamModeButton");
                var modelBounds=model.TransformToAncestor(window).TransformBounds(new Rect(model.RenderSize));var badgeBounds=badge.TransformToAncestor(window).TransformBounds(new Rect(badge.RenderSize));
                Check.True(!modelBounds.IntersectsWith(badgeBounds),"Mode overlaps model selector at minimum width");
                Render(window,Path.Combine(output,$"cam-colors-narrow-{(dark?"dark":"light")}.png"));
                using var review=new CamColorReviewWindow(CamColorTests.Fixture(),new ColorPreviewClient()){Owner=window,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual};
                review.Show();await Layout(review);Render(review,Path.Combine(output,$"cam-colors-review-{(dark?"dark":"light")}.png"));review.Close();
                var palette=new CamColorPaletteWindow(TopSolid.Automation.Mcp.Contracts.CamColorStandard.Starter()){Owner=window,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual};
                palette.Show();await Layout(palette);Render(palette,Path.Combine(output,$"cam-colors-palette-{(dark?"dark":"light")}.png"));palette.Close();
            }
            Check.Equal(0,((StdioMcpClient)Field(window,"mcp")!).Tools.Count,"Toggling modes connected MCP");
            foreach(var language in new[]{"en","ko","fr","ja","es","pt"}) {
                StudioStrings.Apply(language);await Layout(window);
                Check.Equal(StudioStrings.Get("Context.Files"),((MenuItem)add.ContextMenu.Items[0]).Header,"Files menu did not follow UI language");
                var cad=Control<Button>(window,"CadModeButton");var cam=Control<Button>(window,"CamModeButton");
                Check.True(cad.Focusable&&cam.Focusable&&add.Focusable,"Composer controls are not keyboard focusable");
                cam.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Layout(window);
                Check.True(cam.Visibility==Visibility.Collapsed&&!((MenuItem)add.ContextMenu.Items[2]).IsChecked&&cad.IsVisible,"Badge disable did not synchronize menu/state");
                ((MenuItem)add.ContextMenu.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Layout(window);
                var model=Control<ComboBox>(window,"ModelBox");
                Check.True(!cam.TransformToAncestor(window).TransformBounds(new Rect(cam.RenderSize)).IntersectsWith(model.TransformToAncestor(window).TransformBounds(new Rect(model.RenderSize))),"Localized composer overlaps model selector");
            }
            StudioStrings.Apply("en");
        }
        finally {window.Close();if(Directory.Exists(isolated))Directory.Delete(isolated,true);}
        Console.WriteLine("PASS CAM color UI: themes, mode persistence, Access separation, busy state, minimum width and review/editor rendering.");
    }
    private sealed class ColorPreviewClient:IGraphicPreviewClient
    {
        public Task<JObject> GetGraphicPreviewAsync(JObject target,CancellationToken token)=>Task.FromResult(new JObject{["status"]="unavailable"});
    }
}
