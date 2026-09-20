using System.Windows;
using System.Windows.Media.Media3D;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed partial class GraphicPreviewPane
{
    internal void ShowColorBounds(IEnumerable<JArray> bounds)
    {
        var lines=new List<(Point3D,Point3D)>();
        foreach(var box in bounds.Take(256))
        {
            if(box.Count!=6 || box.Any(v=>v.Type is not (JTokenType.Float or JTokenType.Integer) || !double.IsFinite((double)v)))continue;
            var p=Enumerable.Range(0,8).Select(i=>new Point3D((double)box[(i&1)==0?0:3]*1000,(double)box[(i&2)==0?1:4]*1000,(double)box[(i&4)==0?2:5]*1000)).ToArray();
            for(var i=0;i<8;i++)foreach(var bit in new[]{1,2,4})if((i&bit)==0)lines.Add((p[i],p[i|bit]));
        }
        if(lines.Count==0){ClearToolpath();return;}
        toolpathScene=new ToolpathPreviewScene(ToolpathPreviewScene.Lines(lines),lines.Count,false);
        gpu?.ShowToolpath(toolpathScene.Geometry);
        pathStatus.Text=StudioStrings.Get("Color.Bounds");pathStatus.Visibility=Visibility.Visible;UpdateCamera();
    }
}
