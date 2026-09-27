using System.Diagnostics;
namespace DuoMix;
public partial class MainWindow
{
    public async Task MonitorCheck()
    {
        var log = new List<string>(); Process? tone = null;
        try
        {
            await SyncMonitors();
            if (MicStrip.Preview?.Active != true) throw new Exception("Microphone preview did not open at launch");
            log.Add("PASS microphone capture-only preview opens before mixing");
            if (MicPicker.SelectedItem is DeviceChoice selected) { for (int i = 0; i < 12; i++) { var rapid = await CaptureMonitor.Microphone(selected.Id); await rapid.DisposeAsync(); } log.Add("PASS twelve immediate capture-open/close cycles complete without the startup/stop race"); }
            tone = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--tone") { UseShellExecute = false, CreateNoWindow = true })!;
            await AddApp(new AppChoice(tone.Id, "Preview test tone"));
            var card = cards.Single(c => c.App.Pid == tone.Id); float peak = 0;
            for (int i = 0; i < 60; i++) { await Task.Delay(20); card.Strip.Tick(); peak = Math.Max(peak, card.Strip.Meter.Peak); }
            if (peak < .01f || !card.Strip.Meter.Active) throw new Exception("Program preview meter did not register tone");
            if (engine != null || OutputMeter.Active || OutputMeter.Peak != 0) throw new Exception("Idle monitoring activated game output");
            log.Add($"PASS idle program meter shows real audio ({peak:F6}); game microphone stays Off with zero activity");
            await Start(); await Task.Delay(300);
            if (monitors.Count != 0 || MicStrip.Preview != null || card.Strip.Source == null || !OutputMeter.Active) throw new Exception("Mix transition did not replace monitoring capture");
            log.Add("PASS mixing replaces preview captures and activates only then the game meter");
            await Stop(); await Task.Delay(100);
            if (MicStrip.Preview?.Active != true || card.Strip.Preview?.Active != true || card.Strip.Source != null || OutputMeter.Active) throw new Exception("Stop did not resume source preview and turn game meter off");
            log.Add("PASS stopping mixing resumes microphone/program previews and turns game meter off");
            var removed = card.Strip.Preview; await RemoveApp(card);
            if (removed?.Active == true || cards.Contains(card)) throw new Exception("Removed program still capturing");
            shutdown = true; await Stop(); if (monitors.Count != 0) throw new Exception("Capture survives shutdown");
            log.Add("PASS removing sources and closing release preview capture");
        }
        catch (Exception ex) { log.Add("FAIL " + ex); }
        finally { shutdown = true; await Stop(); if (tone != null) { await tone.WaitForExitAsync(); tone.Dispose(); } }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "monitor-validation.txt"), log);
    }
}
