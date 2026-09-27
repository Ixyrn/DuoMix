using System.Diagnostics;

namespace DuoMix;

public sealed class PeakTracker
{
    readonly object gate = new();
    float pending, latest;
    bool unread;
    long timestamp;
    public float Latest { get { lock (gate) return latest; } }
    public void Push(float peak)
    {
        lock (gate)
        {
            latest = float.IsFinite(peak) ? Math.Max(0, peak) : 0;
            pending = unread ? Math.Max(pending, latest) : latest;
            unread = true;
            timestamp = Stopwatch.GetTimestamp();
        }
    }
    public float Read()
    {
        lock (gate)
        {
            if (unread) { unread = false; return pending; }
            // Hold only until the next audio packet, never apply a cosmetic decay.
            return Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds < 100 ? latest : 0;
        }
    }
}
