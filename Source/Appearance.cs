using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace DuoMix;
public sealed class Preferences
{
    public string? CableRenderId { get; set; }
    public string? CableCaptureId { get; set; }
    public string Theme { get; set; } = "Midnight Blue"; public bool Glass { get; set; } = true; public double Tint { get; set; } = 25; public string? Microphone { get; set; }
    public double MicGain { get; set; } = 100; public bool MicMuted { get; set; }
    public double MasterGain { get; set; } = 100;
    static string FilePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DuoMix", "settings.json");
    public static Preferences Load() { try { if (System.IO.File.Exists(FilePath)) return JsonSerializer.Deserialize<Preferences>(System.IO.File.ReadAllText(FilePath)) ?? new(); var legacy = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CorsMic", "settings.json"); if (System.IO.File.Exists(legacy)) return new() { Microphone = JsonSerializer.Deserialize<string>(System.IO.File.ReadAllText(legacy)) }; } catch { } return new(); }
    public void Save() { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!); var temp = FilePath + ".tmp"; System.IO.File.WriteAllText(temp, JsonSerializer.Serialize(this)); System.IO.File.Move(temp, FilePath, true); }
}
public static class Appearance
{
    public static readonly string[] Names = ["Midnight Blue", "Amethyst", "Emerald", "Rose", "Graphite", "Arctic Light"];
    public static int Apply(Window window, Preferences p)
    {
        int index = Array.IndexOf(Names, p.Theme); if (index < 0) index = 0; bool light = index == 5; string[] accents = ["#2189FF", "#A67AFF", "#22CBA0", "#F774AB", "#A1B2C9", "#146DDC"]; string[] bases = ["#0B1422", "#151025", "#091D1B", "#21121E", "#14171D", "#EDF3FA"];
        var r = Application.Current.Resources;
        void Set(string k, string value) => r[k] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        Set("Accent", accents[index]); Set("Base", bases[index]); Set("Text", light ? "#142239" : "#F3F6FF"); Set("Muted", light ? "#4C607B" : "#A7BEDC"); Set("DisabledText", light ? "#70777E" : "#93999F"); Set("Line", light ? "#B5C5D9" : "#34475E");
        var hwnd = new WindowInteropHelper(window).Handle; if (hwnd == IntPtr.Zero) return 0;
        int dark = light ? 0 : 1; DwmSetWindowAttribute(hwnd, 20, ref dark, 4); int corner = 2; DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
        var baseColor = (Color)ColorConverter.ConvertFromString(bases[index]);
        double tint = Math.Clamp(p.Tint, 0, 100) / 100;
        // WindowChrome otherwise resets the extended glass frame after SourceInitialized.
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window); if (chrome != null) chrome.GlassFrameThickness = new Thickness(0);
        int backdrop = 1; DwmSetWindowAttribute(hwnd, 38, ref backdrop, 4);
        int hr = SetBlur(hwnd, p.Glass, baseColor, (byte)(18 + 110 * tint)) ? 0 : -1;
        bool glass = p.Glass && hr >= 0;
        if (light && glass) { Set("Text", "#FFFFFF"); Set("Muted", "#EFF5FF"); }
        r["TextHalo"] = light && glass ? new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, ShadowDepth = 1, BlurRadius = 3, Opacity = .95 } : null;
        Set("LinkText", light && glass ? "#FFFFFF" : accents[index]); Set("PopupBase", light && glass ? "#273445" : bases[index]); ((MainWindow)window).SetDialogContrast(light && glass);
        int edge = glass ? -1 : 0; var margin = new Margins { Left = edge, Right = edge, Top = edge, Bottom = edge }; DwmExtendFrameIntoClientArea(hwnd, ref margin);
        var target = HwndSource.FromHwnd(hwnd)?.CompositionTarget; if (target != null) target.BackgroundColor = Colors.Transparent;
        void Surface(string key, string color, byte alpha) { var c = (Color)ColorConverter.ConvertFromString(color); c.A = glass ? alpha : (byte)255; r[key] = new SolidColorBrush(c); }
        Surface("Card", light ? "#FFFFFF" : "#14202E", (byte)(28 + 48 * tint));
        Surface("Field", light ? "#E2EAF5" : "#1C2A3C", (byte)(24 + 40 * tint));
        Surface("Line", light ? "#91A6BE" : glass ? "#7189A5" : "#34475E", glass ? (byte)78 : (byte)255);
        var top = Color.FromRgb((byte)Math.Min(255, baseColor.R + 7), (byte)Math.Min(255, baseColor.G + 11), (byte)Math.Min(255, baseColor.B + 22)); if (glass) { baseColor.A = (byte)(8 + 150 * tint); top.A = (byte)(14 + 150 * tint); }
    ((MainWindow)window).SetShellBrush(new LinearGradientBrush(top, baseColor, 65)); return hr;
    }

    static bool SetBlur(IntPtr hwnd, bool enabled, Color color, byte alpha)
    {
        var policy = new AccentPolicy { State = enabled ? 3 : 0, Flags = 0, Color = (uint)(alpha << 24 | color.B << 16 | color.G << 8 | color.R) };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try { Marshal.StructureToPtr(policy, memory, false); var data = new CompositionData { Attribute = 19, Data = memory, Size = Marshal.SizeOf<AccentPolicy>() }; return SetWindowCompositionAttribute(hwnd, ref data); } catch (EntryPointNotFoundException) { return false; } finally { Marshal.FreeHGlobal(memory); }
    }
    [StructLayout(LayoutKind.Sequential)] struct AccentPolicy { public int State, Flags; public uint Color; public int Animation; }
    [StructLayout(LayoutKind.Sequential)] struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionData data);
    [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
}



