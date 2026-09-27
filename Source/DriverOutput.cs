using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DuoMix;


public sealed class DriverOutput : IAsyncDisposable
{
    public const string SinkId = "duomix-driver:v1";
    const uint WriteCode = 0x22A400, QueryCode = 0x226404, ResetCode = 0x22A408;
    readonly SafeFileHandle handle;
    readonly CancellationTokenSource stop = new();
    readonly Limiter source;
    Task pump = Task.CompletedTask;
    readonly byte[] status = new byte[24];
    public event Action<string>? Failed;
    public DriverOutput(Limiter source)
    {
        this.source = source;
        handle = CreateFile(@"\\.\DuoMixAudio", 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error, "DuoMix audio driver is unavailable or already in use."); }
        try
        {
            Query();
            if (ReadStatus(0) != 1 || ReadStatus(4) != 48000 || ReadStatus(8) != 2 || ReadStatus(12) != 32) throw new InvalidOperationException("The DuoMix driver has an incompatible audio protocol.");
            Call(ResetCode, null, 0, null, 0);
        }
        catch { handle.Dispose(); stop.Dispose(); throw; }
    }
    public void Start() => pump = Task.Run(Pump);
    uint ReadStatus(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(status.AsSpan(offset, 4));
    void Query() { if (Call(QueryCode, null, 0, status, status.Length) != status.Length) throw new IOException("Incomplete DuoMix driver status."); }
    int Call(uint code, byte[]? input, int inputLength, byte[]? output, int outputLength)
    {
        if (!DeviceIoControl(handle, code, input, inputLength, output, outputLength, out int returned, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return returned;
    }
    async Task Pump()
    {
        var samples = new float[960]; var bytes = new byte[3840];
        using var tick = new PeriodicTimer(TimeSpan.FromMilliseconds(5));
        var clock = Stopwatch.StartNew(); double nextIdle = 0;
        try
        {
            while (await tick.WaitForNextTickAsync(stop.Token))
            {
                Query(); bool running = ReadStatus(20) != 0; int queued = (int)ReadStatus(16);
                if (queued < 0 || queued > 1920) throw new IOException("Invalid DuoMix driver queue size.");
                int frames = running ? Math.Clamp(1440 - queued, 0, 960) : clock.Elapsed.TotalMilliseconds >= nextIdle ? 480 : 0;
                if (!running && frames > 0) nextIdle = clock.Elapsed.TotalMilliseconds + 10;
                while (frames > 0)
                {
                    int count = Math.Min(480, frames) * 2;
                    source.Read(samples.AsSpan(0, count)); Encode(samples.AsSpan(0, count), bytes);
                    Call(WriteCode, bytes, count * 4, null, 0); frames -= count / 2;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { Failed?.Invoke(ex.Message); }
    }
    internal static void Encode(ReadOnlySpan<float> samples, Span<byte> bytes)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            double f = float.IsFinite(samples[i]) ? Math.Clamp((double)samples[i], -1, 1) : 0;
            int value = f <= -1 ? int.MinValue : (int)Math.Round(f * int.MaxValue);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(i * 4, 4), value);
        }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); await pump;
        try { Call(ResetCode, null, 0, null, 0); }
        catch (Win32Exception) { }
        finally { handle.Dispose(); stop.Dispose(); }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] static extern bool DeviceIoControl(SafeFileHandle device, uint code, byte[]? input, int inputLength, byte[]? output, int outputLength, out int returned, IntPtr overlapped);
}
