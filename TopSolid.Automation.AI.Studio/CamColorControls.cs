using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TopSolid.Automation.AI.Studio;

internal static class CamColorControls
{
    internal static void Apply(DataGrid grid)
    {
        grid.SetResourceReference(Control.BackgroundProperty,"SurfaceBrush");grid.SetResourceReference(Control.ForegroundProperty,"TextBrush");
        grid.SetResourceReference(Control.BorderBrushProperty,"BorderBrush");grid.SetResourceReference(DataGrid.RowBackgroundProperty,"SurfaceBrush");
        grid.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty,"PanelBrush");grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty,"BorderBrush");
        grid.SetResourceReference(DataGrid.VerticalGridLinesBrushProperty,"BorderBrush");grid.HeadersVisibility=DataGridHeadersVisibility.Column;
        grid.ColumnHeaderStyle=new Style(typeof(DataGridColumnHeader)){Setters={
            new Setter(Control.BackgroundProperty,new DynamicResourceExtension("PanelBrush")),new Setter(Control.ForegroundProperty,new DynamicResourceExtension("TextBrush")),
            new Setter(Control.PaddingProperty,new Thickness(7,5,7,5))}};
    }
}
