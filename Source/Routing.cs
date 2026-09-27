using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
namespace DuoMix;

public record CableRoute(DeviceChoice Input, DeviceChoice Output);
public static class CableRouting
{
    public static CableRoute? Resolve(Preferences prefs, List<DeviceChoice> inputs, List<DeviceChoice> outputs)
    {
        if (prefs.CableRenderId != null || prefs.CableCaptureId != null)
        {
            var i = inputs.FirstOrDefault(d => d.Id == prefs.CableRenderId); var o = outputs.FirstOrDefault(d => d.Id == prefs.CableCaptureId);
            return i == null || o == null ? null : new(i, o);
        }
        foreach (var o in outputs.Where(d => Devices.Cable(d) && !Devices.OwnDriver(d)).OrderByDescending(d => d.Name.StartsWith("DuoMix")).ThenBy(d => d.Name))
        {
            var peers = inputs.Where(d => Devices.Cable(d) && d.Adapter == o.Adapter).ToList();
            if (peers.Count == 1 && outputs.Count(d => d.Adapter == o.Adapter) == 1) return new(peers[0], o);
        }
        return null;
    }
}
public partial class MainWindow
{
    public void SetDialogContrast(bool enabled) { foreach (var key in new[] { "Text", "Muted", "TextHalo", "PopupBase" }) Dialog.Resources.Remove(key); if (enabled) { Dialog.Resources["Text"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 34, 57)); Dialog.Resources["Muted"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 68, 88)); Dialog.Resources["TextHalo"] = new System.Windows.Media.Effects.DropShadowEffect { Opacity = 0 }; Dialog.Resources["PopupBase"] = System.Windows.Media.Brushes.White; } }
    CableRoute? mixingRoute;
    CableRoute? CurrentRoute() => engine != null ? mixingRoute : CableRouting.Resolve(prefs, Devices.List(DataFlow.Render), Devices.List(DataFlow.Capture));
    void RouteClick(object sender, RoutedEventArgs e)
    {
        if (engine != null || busy) { Status.Text = "Stop mixing before changing the virtual cable."; return; }
        SettingsPanel.Visibility = PickerPanel.Visibility = RenamePanel.Visibility = Visibility.Collapsed; RoutingPanel.Visibility = Visibility.Visible; DialogTitle.Text = "Game microphone connection";
        RouteRender.ItemsSource = Devices.List(DataFlow.Render); RouteCapture.ItemsSource = Devices.List(DataFlow.Capture);
        var route = CurrentRoute(); RouteRender.SelectedItem = ((List<DeviceChoice>)RouteRender.ItemsSource).FirstOrDefault(d => d.Id == (prefs.CableRenderId ?? route?.Input.Id)); RouteCapture.SelectedItem = ((List<DeviceChoice>)RouteCapture.ItemsSource).FirstOrDefault(d => d.Id == (prefs.CableCaptureId ?? route?.Output.Id));
        RouteAuto.IsChecked = prefs.CableRenderId == null && prefs.CableCaptureId == null; RouteManual.IsChecked = !RouteAuto.IsChecked; RouteModeChanged(sender, e); RouteError.Text = ""; ShowDialogPanel();
    }
    void RouteModeChanged(object sender, RoutedEventArgs e) { if (RouteRender != null) RouteRender.IsEnabled = RouteCapture.IsEnabled = RouteManual.IsChecked == true; }
    async void SaveRouteClick(object sender, RoutedEventArgs e)
    {
        if (busy || engine != null) return;
        if (RouteManual.IsChecked == true && (RouteRender.SelectedItem is not DeviceChoice || RouteCapture.SelectedItem is not DeviceChoice)) { RouteError.Text = "Choose both ends of your virtual cable."; return; }
        prefs.CableRenderId = RouteManual.IsChecked == true ? (RouteRender.SelectedItem as DeviceChoice)?.Id : null;
        prefs.CableCaptureId = RouteManual.IsChecked == true ? (RouteCapture.SelectedItem as DeviceChoice)?.Id : null;
        Save(); RefreshDevices(); HideDialog(); await SyncMonitors(); Status.Text = CurrentRoute() == null ? "No cable connected. Install a cable or choose its endpoints." : "Virtual cable selection saved.";
    }
    void CableWebsiteClick(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true });
    async void InstallCableClick(object sender, RoutedEventArgs e)
    {
        if (busy || engine != null) return;
        var installer = System.IO.Path.Combine(AppContext.BaseDirectory, "VirtualCable", "VBCABLE_Setup_x64.exe");
        if (!System.IO.File.Exists(installer)) { CableWebsiteClick(sender, e); return; }
        busy = true;
        try { using var p = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true, Verb = "runas", WorkingDirectory = System.IO.Path.GetDirectoryName(installer)! }); if (p != null) await p.WaitForExitAsync(); RefreshDevices(); await SyncMonitors(); busy = false; RouteClick(sender, e); RouteError.Text = "After installation, restart Windows if requested, then refresh DuoMix."; }
        catch (Exception ex) { RouteError.Text = "Installer not started or completed: " + ex.Message; }
        finally { busy = false; FinishPendingClose(); }
    }
}


