using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
namespace DuoMix;
public sealed class LevelMeter : FrameworkElement
{
    public float Peak { get; set; }
    public bool Active { get; set; }
    public bool IsMuted { get; set; }
    public string DisplayText => !Active ? "Off" : IsMuted ? "Muted" : Peak < 0.001f ? "Silent" : $"{20 * Math.Log10(Peak):0} dBFS";
    public LevelMeter() { Height = 44; MinWidth = 95; ToolTip = "Digital peak level (dBFS). 0 dBFS is full scale; Silent means below −60 dBFS."; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); var muted = (Brush)FindResource("Muted"); var line = (Brush)FindResource("Line"); double db = Peak > 0.0001 && Active && !IsMuted ? 20 * Math.Log10(Peak) : -80; int active = (int)Math.Clamp((db + 60) / 60 * 26, 0, 26); double step = ActualWidth / 26;
        for (int i = 0; i < 26; i++) { var brush = i < active ? new SolidColorBrush(i < 17 ? Color.FromRgb(18, 225, 124) : i < 23 ? Color.FromRgb(205, 228, 30) : Color.FromRgb(255, 87, 102)) : line; dc.DrawRoundedRectangle(brush, null, new Rect(i * step, (ActualHeight - 18) / 2, Math.Max(2, step - 3), 18), 2, 2); }
        var text = new FormattedText(DisplayText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip); dc.DrawText(text, new Point(Math.Max(0, ActualWidth - text.Width), ActualHeight - 13));
    }
}
public sealed class SourceStrip : Grid
{
    public AudioSource? Source; public CaptureMonitor? Preview; public Slider Gain { get; } = new() { Value = 100 }; public CheckBox Mute { get; } = new() { Content = "Mute", VerticalAlignment = VerticalAlignment.Center };
    public LevelMeter Meter { get; } = new(); public event Action? Changed; public bool EnabledSource = true;
    readonly TextBlock percent = new() { Text = "100%", TextAlignment = TextAlignment.Center };
    public SourceStrip()
    {
        foreach (var width in new[] { new GridLength(110), new GridLength(1, GridUnitType.Star), new GridLength(56), new GridLength(88), new GridLength(150) }) ColumnDefinitions.Add(new() { Width = width });
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; labels.Children.Add(new TextBlock { Text = "Microphone", FontSize = 14, FontWeight = FontWeights.SemiBold }); var sub = new TextBlock { Text = "Your voice in the mix", FontSize = 13, Margin = new(0, 4, 0, 0) }; sub.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Children.Add(labels);
        Gain.Margin = new(8, 0, 12, 0); SetColumn(Gain, 1); Children.Add(Gain); var box = new Border { CornerRadius = new(7), BorderThickness = new(1), Padding = new(5, 5, 5, 5), VerticalAlignment = VerticalAlignment.Center, Child = percent }; box.SetResourceReference(Border.BackgroundProperty, "Field"); box.SetResourceReference(Border.BorderBrushProperty, "Line"); SetColumn(box, 2); Children.Add(box); Mute.Margin = new(14, 0, 0, 0); SetColumn(Mute, 3); Children.Add(Mute); SetColumn(Meter, 4); Children.Add(Meter);
        Gain.ValueChanged += (_, _) => { percent.Text = $"{Gain.Value:0}%"; Apply(); Changed?.Invoke(); }; Mute.Checked += (_, _) => { Apply(); Changed?.Invoke(); }; Mute.Unchecked += (_, _) => { Apply(); Changed?.Invoke(); }; AutomationProperties.SetName(Gain, "Source volume"); AutomationProperties.SetName(Mute, "Mute source");
    }
    public void ProgramLayout() { ((TextBlock)((StackPanel)Children[0]).Children[0]).Text = "Program level"; ColumnDefinitions[0].Width = new(100); ColumnDefinitions[4].Width = new(135); }
    public void Apply() { if (Source != null) { Source.Gain = (float)Gain.Value / 100; Source.Muted = Mute.IsChecked == true || !EnabledSource; } }
    public void Tick() { Meter.Active = Source != null || Preview?.Active == true; Meter.IsMuted = Mute.IsChecked == true || !EnabledSource; float measured = Source?.Peaks.Read() ?? (Preview?.Peaks.Read() ?? 0) * (float)Gain.Value / 100; Meter.Peak = Meter.IsMuted ? 0 : measured; Meter.InvalidateVisual(); }

}
public sealed class ProgramCard : Border
{
    public AppChoice App { get; }
    public SourceStrip Strip { get; } = new(); public event Func<ProgramCard, Task>? RemoveRequested;
    public ProgramCard(AppChoice app)
    {
        App = app; CornerRadius = new(11); BorderThickness = new(1); Margin = new(0, 0, 0, 6); SetResourceReference(BackgroundProperty, "Field"); SetResourceReference(BorderBrushProperty, "Line"); var stack = new StackPanel(); Child = stack;
        var head = new Grid { Margin = new(12, 8, 10, 8) }; head.ColumnDefinitions.Add(new() { Width = new(44) }); head.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); head.ColumnDefinitions.Add(new() { Width = new(54) }); head.ColumnDefinitions.Add(new() { Width = new(42) });
        var logo = new Image { Width = 30, Height = 30, Source = ProcessIcon(app.Pid) }; head.Children.Add(logo); var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; var nameLabel = new TextBlock { Text = app.Name, FontSize = 15, FontWeight = FontWeights.SemiBold }; nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "Text"); label.Children.Add(nameLabel); var pid = new TextBlock { Text = $"PID {app.Pid}", FontSize = 11, Margin = new(0, 2, 0, 0) }; pid.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); label.Children.Add(pid); Grid.SetColumn(label, 1); head.Children.Add(label);
        var toggle = new CheckBox { IsChecked = true, Style = (Style)FindResource("Toggle"), VerticalAlignment = VerticalAlignment.Center }; AutomationProperties.SetName(toggle, $"Enable {app.Name}"); toggle.Checked += (_, _) => { Strip.EnabledSource = true; Strip.Apply(); nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "Text"); }; toggle.Unchecked += (_, _) => { Strip.EnabledSource = false; Strip.Apply(); nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "DisabledText"); }; Grid.SetColumn(toggle, 2); head.Children.Add(toggle);
        var arrow = new System.Windows.Shapes.Path { Data = Geometry.Parse("M1,1 L7,7 L13,1"), StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 14, Height = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform() }; arrow.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty, "Text"); var expand = new Button { Content = arrow, ToolTip = "Expand or collapse source", Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(8) }; Grid.SetColumn(expand, 3); head.Children.Add(expand); stack.Children.Add(head);
        var lower = new Border { BorderThickness = new(0, 1, 0, 0), Padding = new(12, 6, 10, 6) }; lower.SetResourceReference(BorderBrushProperty, "Line"); var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new(44) }); Strip.ProgramLayout(); Strip.Margin = new(0, 0, 10, 0); grid.Children.Add(Strip); var remove = new Button { Content = new TextBlock { Text = "\uE74D", FontFamily = new("Segoe MDL2 Assets"), FontSize = 14 }, Padding = new(10), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Remove program" }; AutomationProperties.SetName(remove, $"Remove {app.Name}"); Grid.SetColumn(remove, 1); remove.Click += async (_, _) => { if (RemoveRequested != null) await RemoveRequested(this); }; grid.Children.Add(remove); lower.Child = grid; stack.Children.Add(lower); bool expanded = true; expand.Click += (_, _) => { expanded = !expanded; Motion.Expand(lower, expanded); ((RotateTransform)arrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(expanded ? 0 : -90, TimeSpan.FromMilliseconds(180))); };
    }
    static ImageSource ProcessIcon(int pid) { try { using var p = System.Diagnostics.Process.GetProcessById(pid); var path = p.MainModule?.FileName; if (path != null) { var info = new SHFILEINFO(); SHGetFileInfo(path, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>(), 0x100); if (info.hIcon != IntPtr.Zero) { try { var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); return image; } finally { DestroyIcon(info.hIcon); } } } } catch { } return (ImageSource)Application.Current.FindResource("DuoMixLogo"); }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)] struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attributes; [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)] public string displayName; [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)] public string typeName; }
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
}
