namespace DuoMix;
public partial class MainWindow
{
    readonly SemaphoreSlim monitorGate = new(1, 1);
    readonly List<CaptureMonitor> monitors = [];
    int monitorRevision;
    bool suspendMonitoring, shutdown;
    readonly bool layoutTest = Environment.GetCommandLineArgs().Contains("--ui-check");
    async Task SyncMonitors()
    {
        int revision = ++monitorRevision;
        TraceMonitor($"sync {revision}: wait");
        await monitorGate.WaitAsync();
        try
        {
            if (revision != monitorRevision) return;
            MicStrip.Preview = null; foreach (var card in cards) card.Strip.Preview = null;
            TraceMonitor($"sync {revision}: disposing {monitors.Count}");
            foreach (var monitor in monitors) await monitor.DisposeAsync(); monitors.Clear();
            if (suspendMonitoring || shutdown || closePending || engine != null || layoutTest) return;
            async Task Attach(SourceStrip strip, Func<Task<CaptureMonitor>> create, string label)
            {
                if (revision != monitorRevision) return;
                try
                {
                    TraceMonitor($"sync {revision}: open {label}"); var monitor = await create(); TraceMonitor($"sync {revision}: opened {label}");
                    if (revision != monitorRevision || suspendMonitoring || shutdown) { await monitor.DisposeAsync(); return; }
                    monitors.Add(monitor); strip.Preview = monitor;
                }
                catch (Exception ex) { if (revision == monitorRevision) Status.Text = $"{label} meter unavailable: {ex.Message}"; }
            }
            if (MicPicker.SelectedItem is DeviceChoice mic) await Attach(MicStrip, () => CaptureMonitor.Microphone(mic.Id), "Microphone");
            foreach (var card in cards.ToArray()) if (card.App.Pid > 0) await Attach(card.Strip, () => CaptureMonitor.Program(card.App.Pid), card.App.Name);
        }
        catch (Exception ex) { if (!shutdown) Status.Text = "Source monitoring unavailable: " + ex.Message; }
        finally { monitorGate.Release(); }
    }
    void TraceMonitor(string text) { if (Environment.GetCommandLineArgs().Contains("--monitor-check")) File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "monitor-trace.txt"), text + Environment.NewLine); }
}
