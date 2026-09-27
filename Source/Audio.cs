using NAudio.CoreAudioApi.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace DuoMix;

public record DeviceChoice(string Id, string Name, string Adapter = "") { public override string ToString() => Name; }
public record AppChoice(int Pid, string Name) { public override string ToString() => $"{Name}  ·  PID {Pid}"; }
public static class Devices
{
    public static List<DeviceChoice> List(DataFlow flow)
    {
        using var e = new MMDeviceEnumerator(); var result = new List<DeviceChoice>();
        foreach (var d in e.EnumerateAudioEndPoints(flow, DeviceState.Active)) { using (d) result.Add(new(d.ID, d.FriendlyName, d.DeviceFriendlyName)); }
        return result;
    }
    public static string ShortName(DeviceChoice d) { var suffix = " (" + d.Adapter + ")"; return d.Name.EndsWith(suffix, StringComparison.Ordinal) ? d.Name[..^suffix.Length] : d.Name; }
    public static bool OwnDriver(DeviceChoice d) => d.Adapter.Equals("DuoMix Audio", StringComparison.OrdinalIgnoreCase);
    public static bool Cable(DeviceChoice d) => OwnDriver(d) || d.Adapter.Contains("Voice.ai", StringComparison.OrdinalIgnoreCase) || d.Adapter.Contains("CABLE", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Voice.ai", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase);
    public static DeviceChoice? CableInput() { var recording = CableOutput(); return recording == null ? null : OwnDriver(recording) ? new DeviceChoice(DriverOutput.SinkId, "DuoMix Audio", "DuoMix Audio") : List(DataFlow.Render).FirstOrDefault(d => Cable(d) && d.Adapter == recording.Adapter); }
    public static DeviceChoice? CableOutput() => List(DataFlow.Capture).Where(Cable).OrderByDescending(OwnDriver).ThenByDescending(d => d.Name.StartsWith("DuoMix")).FirstOrDefault();
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant pv);
    public static void Rename(string id, string name)
    {
        name = name.Trim(); if (name.Length == 0 || name.Length > 64 || name.Any(char.IsControl)) throw new ArgumentException("Enter a name of 1–64 characters without control characters.");
        using var e = new MMDeviceEnumerator(); using var d = e.GetDevice(id);

        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariant>());
        for (int i = 0; i < Marshal.SizeOf<PropVariant>(); i++) Marshal.WriteByte(memory, i, 0);
        Marshal.WriteInt16(memory, 31); Marshal.WriteIntPtr(memory, 8, Marshal.StringToCoTaskMemUni(name));
        var value = Marshal.PtrToStructure<PropVariant>(memory); Marshal.FreeHGlobal(memory);
        try { EndpointName.Set(id, ref value); } finally { PropVariantClear(ref value); }
    }
    public static List<AppChoice> Apps()
    {
        var ids = new HashSet<int>();
        using var e = new MMDeviceEnumerator();
        foreach (var d in e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) using (d)
            {
                try { var sessions = d.AudioSessionManager.Sessions; for (int i = 0; i < sessions.Count; i++) { using var s = sessions[i]; ids.Add((int)s.GetProcessID); } } catch { }
            }
        foreach (var p in Process.GetProcesses()) using (p) { try { if (p.MainWindowHandle != IntPtr.Zero) ids.Add(p.Id); } catch { } }
        var result = new List<AppChoice>();
        foreach (var id in ids) { if (id == 0 || id == Environment.ProcessId) continue; try { using var p = Process.GetProcessById(id); result.Add(new(id, p.ProcessName)); } catch { } }
        return result.OrderBy(x => x.Name).ToList();
    }
}
public sealed class AudioSource : ISampleProvider, IAsyncDisposable
{
    readonly WasapiRecorder recorder; readonly Queue<float> queue = new(); readonly object gate = new();
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    public volatile float Gain = 1; public readonly PeakTracker Peaks = new(); public float Peak => Peaks.Latest; public volatile bool Muted;
    public event Action<string>? Failed;
    public AudioSource(WasapiRecorder recorder)
    {
        this.recorder = recorder;
        recorder.DataAvailable += (data, flags, _, _) =>
        {
            var samples = MemoryMarshal.Cast<byte, float>(data);
            bool silent = (flags & AudioClientBufferFlags.Silent) != 0;
            lock (gate) { if (queue.Count + samples.Length > 19200) queue.Clear(); foreach (var f in samples) queue.Enqueue(!silent && float.IsFinite(f) ? f : 0); }
        };
        recorder.RecordingStopped += (s, e) => { if (e.Exception != null) Failed?.Invoke(e.Exception.Message); };
    }
    public void Start() => recorder.StartRecording();
    public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public int Read(Span<float> buffer)
    {
        int count = buffer.Length;
        float gain = Muted ? 0 : Gain;
        float peak = 0;
        lock (gate) { for (int i = 0; i < count; i++) { buffer[i] = (queue.Count > 0 ? queue.Dequeue() : 0) * gain; peak = Math.Max(peak, Math.Abs(buffer[i])); } }
        Peaks.Push(peak);
        return count;
    }
    public async ValueTask DisposeAsync() { await RecorderLifetime.Close(recorder); }
}
public sealed class Limiter(ISampleProvider source) : ISampleProvider
{
    public WaveFormat WaveFormat => source.WaveFormat; public readonly PeakTracker Peaks = new(); public float Peak => Peaks.Latest; public volatile float Gain = 0.8f;
    public int Read(float[] b, int o, int c) => Read(b.AsSpan(o, c));
    public int Read(Span<float> b) { int n = source.Read(b); float peak = 0; for (int i = 0; i < n; i++) { b[i] = Math.Clamp(b[i] * Gain, -0.98f, 0.98f); peak = Math.Max(peak, Math.Abs(b[i])); } Peaks.Push(peak); return n; }
}
public sealed class MixerEngine : IAsyncDisposable
{
    readonly MixingSampleProvider mix = new(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };
    readonly List<AudioSource> sources = new(); WasapiPlayer? player; DriverOutput? driver; MMDevice? output;
    public Limiter Master { get; }
    public event Action<string>? Failed;
    public MixerEngine() { Master = new(mix); }
    public async Task<AudioSource> AddMic(string id)
    {
        using var e = new MMDeviceEnumerator(); using var d = e.GetDevice(id);
        return Attach(await new WasapiRecorderBuilder().WithDevice(d).WithFormat(mix.WaveFormat).WithBufferLength(30).BuildAsync());
    }
    public async Task<AudioSource> AddApp(int pid) => Attach(await new WasapiRecorderBuilder().WithProcessLoopback((uint)pid, ProcessLoopbackMode.IncludeTargetProcessTree).WithFormat(mix.WaveFormat).WithBufferLength(30).BuildAsync());
    AudioSource Attach(WasapiRecorder r) { var s = new AudioSource(r); s.Failed += m => Failed?.Invoke(m); sources.Add(s); mix.AddMixerInput(s); s.Start(); return s; }
    public async Task Remove(AudioSource s) { mix.RemoveMixerInput(s); sources.Remove(s); await s.DisposeAsync(); }
    public void Start(string id)
    {
        if (id == DriverOutput.SinkId) { driver = new DriverOutput(Master); driver.Failed += message => Failed?.Invoke(message); driver.Start(); return; }
        using var e = new MMDeviceEnumerator(); output = e.GetDevice(id);
        player = new WasapiPlayerBuilder().WithDevice(output).WithLatency(40).Build();
        player.PlaybackStopped += (s, a) => { if (a.Exception != null) Failed?.Invoke(a.Exception.Message); };
        player.Init(Master.ToWaveProvider()); player.Play();
    }
    public async ValueTask DisposeAsync() { if (driver != null) { await driver.DisposeAsync(); driver = null; } if (player != null) { await player.DisposeAsync(); player = null; } foreach (var s in sources) await s.DisposeAsync(); sources.Clear(); output?.Dispose(); }
}
