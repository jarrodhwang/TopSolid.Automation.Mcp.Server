using System.Windows;
using System.Windows.Controls;

namespace TopSolid.Automation.AI.Studio.Preview;

internal static class PreviewLayout
{
    internal static Grid Wrap(Window window, FrameworkElement details, GraphicPreviewPane preview, bool narrowTabs = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(details); grid.Children.Add(preview);
        // A stacked viewport would consume the picker at small sizes. Tabs keep
        // search/selection and the complete viewport reachable, with one action row.
        var tabs = new TabControl { Visibility = Visibility.Collapsed, Padding = new Thickness(0), BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 8) };
        var selectionTab = new TabItem(); selectionTab.SetResourceReference(HeaderedContentControl.HeaderProperty, "Ui.Question.Selection");
        var previewTab = new TabItem(); previewTab.SetResourceReference(HeaderedContentControl.HeaderProperty, "Ui.Preview.Title");
        if (narrowTabs)
        {
            tabs.Items.Add(selectionTab); tabs.Items.Add(previewTab); tabs.SelectedIndex = 0;
            Grid.SetColumnSpan(tabs, 2); grid.Children.Add(tabs);
            tabs.SelectionChanged += (_, args) =>
            {
                if (!ReferenceEquals(args.Source, tabs) || tabs.Visibility != Visibility.Visible) return;
                details.Visibility = tabs.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
                preview.Visibility = tabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            };
        }
        window.Width = Math.Min(1080, SystemParameters.WorkArea.Width - 40);
        window.Height = Math.Min(740, SystemParameters.WorkArea.Height - 40);
        window.MinHeight = Math.Min(600, window.Height);
        var previous = (bool?)null;
        void Reflow()
        {
            var wide = window.ActualWidth >= 860;
            if (wide == previous) return; previous = wide;
            if (narrowTabs)
            {
                // Keep controls parented so resizing changes layout only,
                // retaining input focus, exact selection and preview lifetime.
                if (!wide)
                {
                    tabs.Visibility = Visibility.Visible;
                    grid.ColumnDefinitions[1].Width = new GridLength(0);
                    grid.RowDefinitions[0].Height = GridLength.Auto;
                    grid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                    Grid.SetRow(details, 1); Grid.SetRow(preview, 1); Grid.SetColumn(preview, 0);
                    preview.Margin = details.Margin = new Thickness(0);
                    details.Visibility = tabs.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
                    preview.Visibility = tabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
                    return;
                }
                tabs.Visibility = Visibility.Collapsed;
                details.Visibility = preview.Visibility = Visibility.Visible;
                details.Margin = new Thickness(0);
                Grid.SetRow(preview, 0);
            }
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
