using System.Windows;
namespace DuoMix;
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--tone")) { Diagnostics.Tone().GetAwaiter().GetResult(); return; }
        if (args.Contains("--diagnose")) { Diagnostics.Run().GetAwaiter().GetResult(); return; }
        if (args.Contains("--meter-check")) { Diagnostics.CheckMeters().GetAwaiter().GetResult(); return; }
        if (args.Contains("--rename")) { var device = Devices.CableOutput(); if (device != null) Devices.Rename(device.Id, "DuoMix"); return; }
        var app = new Application(); app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("Theme.xaml", UriKind.Relative) }); if (args.Length > 1 && args[0] == "--export-logo") { LogoExport.Write(args[1]); return; }
        var window = new MainWindow();
        if (args.Contains("--glass-test")) { var fixture = MaterialFixture.Create(); fixture.Show(); window.Owner = fixture; window.Closed += (_, _) => fixture.Close(); }
        if (args.Contains("--preview")) window.Loaded += async (_, _) => { await window.Preview(); };
        if (args.Contains("--snapshot")) window.Loaded += async (_, _) => { await window.Preview(); await Task.Delay(500); window.Snapshot(System.IO.Path.Combine(AppContext.BaseDirectory, "preview.png")); window.Close(); };
        if (args.Contains("--ui-check")) window.Loaded += async (_, _) => { await window.UiCheck(); window.Close(); };
        if (args.Contains("--monitor-check")) window.Loaded += async (_, _) => { await window.MonitorCheck(); window.Close(); };
        app.Run(window);
    }
}
