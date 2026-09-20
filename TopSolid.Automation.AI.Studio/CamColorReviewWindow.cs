using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamColorReviewWindow : Window, IDisposable
{
    private readonly CamColorPlan plan;
    private readonly GraphicPreviewPane preview;
    private readonly List<Row> rows;
    private readonly DataGrid grid;
    internal CamColorPlan? Result {get;private set;}
    internal sealed class Row : INotifyPropertyChanged
    {
        private string role="";
        public required JObject Source {get;init;}
        public required CamColorStandard Palette {get;init;}
        public bool DisplaySupported { get; init; } = true;
        private bool include;
        public bool Include {get=>include;set{include=value&&Supported;PropertyChanged?.Invoke(this,new(nameof(Include)));}}
        public string Target => (string?)Source["name"]??"";
        public string Kind => StudioStrings.Get("Color.Kind."+(string?)Source["kind"]);
        public string Scope => (string?)Source["geometry"]?["colorScope"]=="wholeSketch"?StudioStrings.Get("Color.WholeSketch"):Kind;
        public string Before => ColorText(Source["color"]);
        public string RoleKey {get=>role;set{role=value;PropertyChanged?.Invoke(this,new(nameof(RoleKey)));PropertyChanged?.Invoke(this,new(nameof(After)));}}
        public string After => Palette.Roles.FirstOrDefault(r=>r.Key==role)?.Hex??"—";
        public string Group {get;set;}="";
        public string Reason {get;set;}="";
        public bool Supported => DisplaySupported && (bool?)Source["colorSupported"]==true;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    internal CamColorReviewWindow(CamColorPlan plan, IGraphicPreviewClient client)
    {
        this.plan=plan; Title=StudioStrings.Get("Color.Review");Width=1160;Height=780;MinWidth=850;MinHeight=560;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;Icon=TopSolidIcons.Get("color");
        TopSolidTheme.ApplyWindow(this);SetResourceReference(BackgroundProperty,"WindowBrush");SetResourceReference(ForegroundProperty,"TextBrush");
        rows=plan.Geometry.OfType<JObject>().Select(source=> {
            var assignment=plan.Assignments.FirstOrDefault(a=>a.TargetKey==(string?)source["key"]);
            return new Row {Source=source,Palette=plan.Palette,Include=assignment!=null,RoleKey=assignment?.RoleKey??"",Group=assignment?.Group??"",
                Reason=(bool?)source["colorSupported"]!=true?StudioStrings.Get("Color.Unsupported")+": "+(string?)source["supportReason"]:assignment?.Reason??(string?)source["proposalReason"]??StudioStrings.Get("Color.Unassigned")};
        }).ToList();
        var root=new DockPanel {Margin=new Thickness(14)};Content=root;
        var heading=new TextBlock {Text=plan.DocumentName+" · "+plan.Palette.Name+(plan.Palette.Id==CamColorStandard.BuiltInId?"":" · v"+plan.Palette.Version),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var counts=new TextBlock {Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(counts,Dock.Top);root.Children.Add(counts);
        void RefreshCounts() => counts.Text=StudioStrings.Get("Color.Counts",rows.Count(r=>r.Include&&r.Source["kind"]?.ToString()=="face"),rows.Count(r=>r.Include&&r.Source["kind"]?.ToString()!="face"),rows.Count);
        foreach(var row in rows) row.PropertyChanged+=(_,_)=>RefreshCounts();RefreshCounts();
        var status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};
        status.SetResourceReference(ForegroundProperty,"DangerBrush");DockPanel.SetDock(status,Dock.Bottom);
        var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(status);
        var cancel=new Button {Content=StudioStrings.Text("Cancel"),IsCancel=true,Padding=new Thickness(12,6,12,6),Margin=new Thickness(4)};footer.Children.Add(cancel);
        var apply=new Button {Content=StudioStrings.Get("Color.Apply"),Padding=new Thickness(12,6,12,6),Margin=new Thickness(4)};footer.Children.Add(apply);
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
        root.Children.Add(layout);
        preview=new GraphicPreviewPane(client,new JObject {["documentId"]=plan.DocumentId});layout.Children.Add(preview);
        var bottom=new DockPanel {Margin=new Thickness(0,8,0,0)};Grid.SetRow(bottom,1);layout.Children.Add(bottom);
        var options=new[] {new {Key="",Display=StudioStrings.Get("Color.Unassigned")}}.Concat(plan.Palette.Roles.Select(r=>new {r.Key,Display=(plan.Palette.Id==CamColorStandard.BuiltInId?StudioStrings.Get("Color.Role."+r.Key):r.Label)+"  "+r.Hex})).ToArray();
        var toolbar=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,6)};DockPanel.SetDock(toolbar,Dock.Top);bottom.Children.Add(toolbar);
        var roles=new ComboBox {ItemsSource=options,DisplayMemberPath="Display",SelectedValuePath="Key",SelectedIndex=0,Width=240};toolbar.Children.Add(roles);
        var group=new TextBox {Width=140,Margin=new Thickness(6,0,6,0),MaxLength=80,ToolTip=StudioStrings.Get("Color.Group")};toolbar.Children.Add(group);
        var assign=new Button {Content=StudioStrings.Get("Color.AssignSelected"),Padding=new Thickness(8,3,8,3)};toolbar.Children.Add(assign);
        grid=new DataGrid {ItemsSource=rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Extended,SelectionUnit=DataGridSelectionUnit.FullRow};
        CamColorControls.Apply(grid);
        var checkbox=new Style(typeof(CheckBox));checkbox.Setters.Add(new Setter(IsEnabledProperty,new Binding(nameof(Row.Supported))));checkbox.Setters.Add(new Setter(HorizontalAlignmentProperty,HorizontalAlignment.Center));
        grid.Columns.Add(new DataGridCheckBoxColumn {Header=StudioStrings.Get("Color.Include"),Binding=new Binding(nameof(Row.Include)),ElementStyle=checkbox,EditingElementStyle=checkbox});
        grid.Columns.Add(Text("Color.Target",nameof(Row.Target),170));grid.Columns.Add(Text("Color.Scope",nameof(Row.Scope),100));grid.Columns.Add(Text("Color.Before",nameof(Row.Before),85));
        grid.Columns.Add(new DataGridComboBoxColumn {Header=StudioStrings.Get("Color.Role"),ItemsSource=options,DisplayMemberPath="Display",SelectedValuePath="Key",SelectedValueBinding=new Binding(nameof(Row.RoleKey)),Width=210});
        grid.Columns.Add(Text("Color.After",nameof(Row.After),85));grid.Columns.Add(Text("Color.Group",nameof(Row.Group),100,false));grid.Columns.Add(Text("Color.Reason",nameof(Row.Reason),280));bottom.Children.Add(grid);
        assign.Click+=(_,_)=> {
            grid.CommitEdit(); foreach(var row in grid.SelectedItems.OfType<Row>().Where(r=>r.Supported)) {row.RoleKey=(string?)roles.SelectedValue??"";row.Group=group.Text;row.Include=row.RoleKey.Length>0;}
            grid.Items.Refresh();
        };
        grid.SelectionChanged+=(_,_)=>ShowBounds();
        Loaded+=async (_,_)=> {await preview.ReloadAsync();ShowBounds();};
        apply.Click+=(_,_)=> {
            try {
                if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))return;
                plan.Assignments=rows.Where(r=>r.Include).Select(r=>new CamColorAssignment {TargetKey=(string)r.Source["key"]!,RoleKey=r.RoleKey,Group=r.Group,Reason=r.Reason}).ToList();
                plan.ToArguments(); Result=plan;DialogResult=true;
            }catch(ArgumentException ex){status.Text=ex.Message;}
        };
    }
    private void ShowBounds() => preview.ShowColorBounds(grid.SelectedItems.OfType<Row>().Select(r=>r.Source["geometry"]?["bounds"]).OfType<JArray>());
    private static DataGridTextColumn Text(string key,string property,int width,bool readOnly=true)=>new(){Header=StudioStrings.Get(key),Binding=new Binding(property),Width=width,IsReadOnly=readOnly};
    private static string ColorText(JToken? value)=>value?["r"]!=null?$"#{(int)value["r"]!:X2}{(int)value["g"]!:X2}{(int)value["b"]!:X2}":"—";
    public void Dispose()=>preview.Dispose();
}
