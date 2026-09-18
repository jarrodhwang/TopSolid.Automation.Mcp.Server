using System.Windows;
using System.Windows.Controls;

namespace TopSolid.Automation.AI.Studio.Preview;

internal static class PreviewLayout
{
    internal static Grid Wrap(Window window, FrameworkElement details, GraphicPreviewPane preview)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(details); grid.Children.Add(preview);
        window.Width = Math.Min(1080, SystemParameters.WorkArea.Width - 40);
        window.Height = Math.Min(740, SystemParameters.WorkArea.Height - 40);
        window.MinHeight = Math.Min(600, window.Height);
        var previous = (bool?)null;
        void Reflow()
        {
            var wide = window.ActualWidth >= 860;
            if (wide == previous) return; previous = wide;
            grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            grid.ColumnDefinitions[1].Width = wide ? new GridLength(1.25, GridUnitType.Star) : new GridLength(0);
            grid.RowDefinitions[0].Height = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(310);
            grid.RowDefinitions[1].Height = wide ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetRow(details, wide ? 0 : 1); Grid.SetColumn(preview, wide ? 1 : 0);
            preview.Margin = wide ? new Thickness(16, 0, 0, 0) : new Thickness(0, 0, 0, 12);
            preview.MinHeight = wide ? 270 : 240;
        }
        window.SizeChanged += (_, _) => Reflow(); window.Closed += (_, _) => preview.Dispose();
        Reflow(); return grid;
    }
}
