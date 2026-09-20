using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TopSolid.Automation.AI.Studio;

internal static class NativeColorPicker
{
    internal static bool TryParse(string? hex,out Color color)
    {
        color=Colors.White;
        if(hex is null || hex.Length!=7 || hex[0]!='#' || !uint.TryParse(hex.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var rgb)) return false;
        color=Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
        return true;
    }

    internal static string? Show(Window owner,string hex)
    {
        TryParse(hex,out var color);
        var custom=Marshal.AllocHGlobal(16*sizeof(int));
        try {
            Marshal.Copy(new int[16],0,custom,16);
            var choice=new ChooseColorData {
                Size=Marshal.SizeOf<ChooseColorData>(),Owner=new WindowInteropHelper(owner).Handle,
                Rgb=color.R | ((uint)color.G<<8) | ((uint)color.B<<16),CustomColors=custom,
                Flags=0x00000001 | 0x00000002 // CC_RGBINIT | CC_FULLOPEN
            };
            if(!ChooseColor(ref choice)) return null;
            return $"#{choice.Rgb & 255:X2}{(choice.Rgb>>8) & 255:X2}{(choice.Rgb>>16) & 255:X2}";
        }
        finally {Marshal.FreeHGlobal(custom);}
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChooseColorData
    {
        public int Size;
        public IntPtr Owner,Instance;
        public uint Rgb;
        public IntPtr CustomColors;
        public uint Flags;
        public IntPtr CustomData,Hook,TemplateName;
    }

    [DllImport("comdlg32.dll",EntryPoint="ChooseColorW",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColor(ref ChooseColorData data);
}
