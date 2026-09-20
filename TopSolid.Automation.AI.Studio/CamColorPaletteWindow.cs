using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamColorPaletteWindow : Window
{
    internal CamColorStandard? Result { get; private set; }
    private readonly ObservableCollection<PaletteRow> roles;
    private readonly CamColorStandard original;
    internal CamColorPaletteWindow(CamColorStandard palette)
    {
        original=palette.Snapshot(); roles=new(original.Roles.Select(role=>new PaletteRow {Key=role.Key,Label=role.Label,Hex=role.Hex}));
        if(original.Id==CamColorStandard.BuiltInId) foreach(var role in roles) role.Label=StudioStrings.Get("Color.Role."+role.Key);
        Title=StudioStrings.Get("Color.EditPalette"); Width=720; Height=500; MinWidth=540; MinHeight=350;
        WindowStartupLocation=WindowStartupLocation.CenterOwner; ShowInTaskbar=false; Icon=TopSolidIcons.Get("color");
        TopSolidTheme.ApplyWindow(this); SetResourceReference(BackgroundProperty,"WindowBrush"); SetResourceReference(ForegroundProperty,"TextBrush");
        var root=new DockPanel { Margin=new Thickness(16) }; Content=root;
        var error=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) }; error.SetResourceReference(ForegroundProperty,"DangerBrush");
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right}; DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons);
        DockPanel.SetDock(error,Dock.Bottom);root.Children.Add(error);
        var grid=new DataGrid {ItemsSource=roles,AutoGenerateColumns=false,CanUserAddRows=true,CanUserDeleteRows=true};
        CamColorControls.Apply(grid);
        grid.Columns.Add(new DataGridTextColumn {Header=StudioStrings.Get("Color.RoleKey"),Binding=new Binding("Key"),Width=140});
        grid.Columns.Add(new DataGridTextColumn {Header=StudioStrings.Get("Color.Role"),Binding=new Binding("Label"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        var swatch=new FrameworkElementFactory(typeof(Button));
        swatch.SetValue(FrameworkElement.WidthProperty,28d);
        swatch.SetValue(FrameworkElement.HeightProperty,20d);
        swatch.SetValue(Control.PaddingProperty,new Thickness(0));
        swatch.SetValue(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Center);
        swatch.SetValue(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center);
        swatch.SetValue(FrameworkElement.MarginProperty,new Thickness(4,2,4,2));
        swatch.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty,new Binding("Label"));
        swatch.SetBinding(FrameworkElement.ToolTipProperty,new Binding("Hex"));
        var fill=new FrameworkElementFactory(typeof(Border));
        fill.SetValue(FrameworkElement.WidthProperty,20d);
        fill.SetValue(FrameworkElement.HeightProperty,12d);
        fill.SetBinding(Border.BackgroundProperty,new Binding("ColorBrush"));
        swatch.AppendChild(fill);
        swatch.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,_)=> {
            if(((FrameworkElement)sender).DataContext is not PaletteRow row) return;
            if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true)) return;
            var selected=NativeColorPicker.Show(this,row.Hex);
            if(selected is not null) row.Hex=selected;
        }));
        grid.Columns.Add(new DataGridTemplateColumn {Header="",CellTemplate=new DataTemplate {VisualTree=swatch},Width=38,CanUserSort=false});
        grid.Columns.Add(new DataGridTextColumn {Header="RGB",Binding=new Binding("Hex") {UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=110});
        root.Children.Add(grid);
        var reset=new Button {Content=StudioStrings.Get("Color.BuiltIn"),Margin=new Thickness(4),Padding=new Thickness(12,6,12,6)};
        reset.Click+=(_,_)=> {Result=CamColorStandard.Starter();DialogResult=true;}; buttons.Children.Add(reset);
        var cancel=new Button {Content=StudioStrings.Text("Cancel"),IsCancel=true,Margin=new Thickness(4),Padding=new Thickness(12,6,12,6)}; buttons.Children.Add(cancel);
        var save=new Button {Content=StudioStrings.Text("Save"),Margin=new Thickness(4),Padding=new Thickness(12,6,12,6)};buttons.Children.Add(save);
        save.Click+=(_,_)=> {
            try {
                if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true)) return;
                var next=new CamColorStandard {Id=original.Id==CamColorStandard.BuiltInId?"custom-"+Guid.NewGuid().ToString("N"):original.Id,
                    Version=original.Id==CamColorStandard.BuiltInId?1:checked(original.Version+1),Name=StudioStrings.Get("Color.Custom"),Roles=roles.Select(row=>new CamColorRole {Key=row.Key,Label=row.Label,Hex=row.Hex}).ToList()};
                next.Validate();Result=next.Snapshot();DialogResult=true;
            } catch(Exception ex) when(ex is ArgumentException or OverflowException) {error.Text=ex.Message;}
        };
    }

    public sealed class PaletteRow : INotifyPropertyChanged
    {
        public string Key {get;set;}="";
        public string Label {get;set;}="";
        private string hex="";
        public string Hex {
            get=>hex;
            set {hex=value;PropertyChanged?.Invoke(this,new(nameof(Hex)));PropertyChanged?.Invoke(this,new(nameof(ColorBrush)));}
        }
        public Brush ColorBrush=>NativeColorPicker.TryParse(Hex,out var color)?new SolidColorBrush(color):Brushes.Transparent;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
