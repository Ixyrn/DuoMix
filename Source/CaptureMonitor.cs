using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace DuoMix;

public sealed class CaptureMonitor : IAsyncDisposable
{
    readonly WasapiRecorder recorder;
    public PeakTracker Peaks { get; } = new();
    public bool Active { get; private set; } = true;
    public string? Error { get; private set; }
    CaptureMonitor(WasapiRecorder recorder)
    {
        this.recorder = recorder;
        recorder.DataAvailable += (data, flags, _, _) =>
        {
            float peak = 0;
            if ((flags & AudioClientBufferFlags.Silent) == 0)
                foreach (float sample in MemoryMarshal.Cast<byte, float>(data))
                    if (float.IsFinite(sample)) peak = Math.Max(peak, Math.Abs(sample));
            Peaks.Push(peak);
        };
        recorder.RecordingStopped += (_, e) => { Active = false; Error = e.Exception?.Message; Peaks.Push(0); };
    }
    static async Task<CaptureMonitor> Open(WasapiRecorderBuilder builder)
    {
        var monitor = new CaptureMonitor(await builder.WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)).WithBufferLength(30).BuildAsync());
        try { monitor.recorder.StartRecording(); return monitor; } catch { await monitor.DisposeAsync(); throw; }
    }
    public static async Task<CaptureMonitor> Microphone(string id) { using var e = new MMDeviceEnumerator(); using var d = e.GetDevice(id); return await Open(new WasapiRecorderBuilder().WithDevice(d)); }
    public static Task<CaptureMonitor> Program(int pid) => Open(new WasapiRecorderBuilder().WithProcessLoopback((uint)pid, ProcessLoopbackMode.IncludeTargetProcessTree));
    public async ValueTask DisposeAsync() { Active = false; await RecorderLifetime.Close(recorder); }
}
