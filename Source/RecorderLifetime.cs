using NAudio.Wave;
namespace DuoMix;
static class RecorderLifetime
{
    public static async ValueTask Close(WasapiRecorder recorder)
    {
        var dispose = recorder.DisposeAsync().AsTask();
        while (!dispose.IsCompleted) { await Task.WhenAny(dispose, Task.Delay(20)); if (!dispose.IsCompleted) recorder.StopRecording(); }
        await dispose;
    }
}
