using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace DuoMix;
public static class Diagnostics
{
    public static async Task CheckMeters()
    {
        var log = new List<string>();
        try
        {
            var input = Devices.CableInput() ?? throw new Exception("Test cable playback missing");
            var output = Devices.CableOutput() ?? throw new Exception("Test cable capture missing");
            using var endpoints = new MMDeviceEnumerator(); using var device = endpoints.GetDevice(input.Id);
            await using var player = new WasapiPlayerBuilder().WithDevice(device).WithLatency(30).Build();
            player.Init(new PulseFixture().ToWaveProvider());
            await using var mixer = new MixerEngine();
            var mic = await mixer.AddMic(output.Id);
            player.Play(); var block = new float[960]; float highest = 0; int audible = 0, silent = 0;
            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(10); mixer.Master.Read(block);
                float sourcePeak = mic.Peaks.Read(), outPeak = mixer.Master.Peaks.Read();
                if (Math.Abs(outPeak - sourcePeak * .8f) > .00001f) throw new Exception("Microphone and master meters disagree with gain");
                float measured = block.Max(x => Math.Abs(x)); if (Math.Abs(outPeak - measured) > .00001f) throw new Exception("Output peak disagrees with actual samples");
                highest = Math.Max(highest, sourcePeak); if (sourcePeak > .02) audible++; else silent++;
            }
            if (highest < .05 || audible < 5 || silent < 5) throw new Exception($"Pulse capture missing: {highest}, audible={audible}, silent={silent}");
            log.Add($"PASS WASAPI microphone-capture path: {audible} audible and {silent} silent blocks; peak {highest:F6}");
            log.Add("PASS microphone meter equals actual source samples; output meter equals post-gain output samples");
            mic.Muted = true; await Task.Delay(30); mixer.Master.Read(block); if (mic.Peaks.Read() != 0 || mixer.Master.Peaks.Read() != 0 || block.Any(x => x != 0)) throw new Exception("Muted samples/meters must be zero");
            log.Add("PASS mute immediately produces zero samples and zero meters");
            log.Add("Fixture used the existing virtual cable as a deterministic microphone input; no acoustic speech measurement implied.");
        }
        catch (Exception ex) { log.Add("FAIL " + ex); }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "meter-validation.txt"), log);
    }
    sealed class PulseFixture : ISampleProvider
    {
        long sample;
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        public int Read(float[] b, int o, int c) => Read(b.AsSpan(o, c));
        public int Read(Span<float> b) { for (int i = 0; i < b.Length; i++, sample++) { long frame = sample / 2; b[i] = frame % 9600 < 2400 ? (float)(.25 * Math.Sin(2 * Math.PI * 440 * frame / 48000)) : 0; } return b.Length; }
    }
    public static async Task Tone() { using var p = new WasapiPlayerBuilder().Build(); p.Init(new SignalGenerator(48000, 2) { Frequency = 523.25, Gain = 0.025, Type = SignalGeneratorType.Sin }.ToWaveProvider()); p.Play(); await Task.Delay(6000); }
    public static async Task Run()
    {
        var log = new List<string>(); try
        {
            var render = Devices.List(DataFlow.Render); var capture = Devices.List(DataFlow.Capture); log.Add(JsonSerializer.Serialize(new { render, capture }));
            var input = Devices.CableInput() ?? throw new Exception("Cable playback missing"); var output = Devices.CableOutput() ?? throw new Exception("Cable recording missing");
            using var e = new MMDeviceEnumerator(); using var d = e.GetDevice(output.Id);
            await using var recorder = await new WasapiRecorderBuilder().WithDevice(d).WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)).BuildAsync();
            float peak = 0; recorder.DataAvailable += (b, _, _, _) => { foreach (var f in MemoryMarshal.Cast<byte, float>(b)) peak = Math.Max(peak, Math.Abs(f)); }; recorder.StartRecording();
            await using (var mixer = new MixerEngine())
            {
                var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--tone") { UseShellExecute = false, CreateNoWindow = true })!;
                var s = await mixer.AddApp(child.Id); mixer.Start(input.Id); await Task.Delay(1800); log.Add($"Application source peak: {s.Peak}; virtual microphone peak: {peak}");
                if (peak < 0.001f) throw new Exception("No signal reached virtual microphone");
                s.Muted = true; await Task.Delay(350); peak = 0; await Task.Delay(300); log.Add($"Muted virtual microphone peak: {peak}"); if (peak > 0.001f) throw new Exception("Mute failed");
                s.Muted = false;
                var child2 = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--tone") { UseShellExecute = false, CreateNoWindow = true })!;
                var second = await mixer.AddApp(child2.Id); await Task.Delay(500); peak = 0; await Task.Delay(350);
                if (s.Peak < 0.001f) throw new Exception("First source missing during simultaneous mix");
                log.Add($"Second source peak: {second.Peak}; mixed output: {peak}");
                if (second.Peak < 0.001f || peak < 0.001f) throw new Exception("Multiple source test failed");
                await mixer.Remove(second); await child2.WaitForExitAsync(); child2.Dispose();
                await child.WaitForExitAsync(); child.Dispose();
                var mic = Devices.List(DataFlow.Capture).FirstOrDefault(x => x.Name.Contains("Focusrite"));
                if (mic != null) { var m = await mixer.AddMic(mic.Id); await Task.Delay(500); log.Add($"PASS: microphone capture opened at 48 kHz stereo ({mic.Name}); peak {m.Peak}"); await mixer.Remove(m); }

            }
            log.Add("PASS: process capture → mixer → virtual cable recording, and mute.");

        }
        catch (Exception ex) { log.Add("FAIL: " + ex); }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "diagnostics.txt"), log);
    }
}
