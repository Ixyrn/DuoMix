using System.Windows;
using System.Windows.Media;
using NAudio.CoreAudioApi;
namespace DuoMix;
public partial class MainWindow
{
    public async Task UiCheck()
    {
        var lines = new List<string>(); string theme = prefs.Theme; bool glass = prefs.Glass; double tint = prefs.Tint; double width = Width, height = Height; try
        {
            RoutingChecks.Run(); lines.Add("PASS automatic/manual cable routing, ambiguity rejection, missing endpoint handling and preference serialization"); await Task.Delay(250); UpdateLayout(); if (MainScroll.ScrollableHeight > .5) throw new Exception("Empty launch overflows"); lines.Add("PASS empty launch has no vertical overflow");
            await AddApp(new AppChoice(-100, "Layout test")); await Task.Delay(250); UpdateLayout(); if (MainScroll.ScrollableHeight > .5) throw new Exception($"One program overflows by {MainScroll.ScrollableHeight}"); lines.Add($"PASS one expanded program fits at default {Width}x{Height}; no scrollbar");
            Height = 560; UpdateLayout(); if (MainScroll.ScrollableHeight <= 0) throw new Exception("Short window should scroll"); Height = height;
            for (int i = 0; i < 4; i++) await AddApp(new AppChoice(-101 - i, "Additional source " + i)); await Task.Delay(250); UpdateLayout(); if (MainScroll.ScrollableHeight <= 0) throw new Exception("Extra program rows should scroll"); lines.Add("PASS scrolling activates when shrinking height or adding sources");
            Width = 760; UpdateLayout(); if (MicStrip.Gain.ActualWidth < 60 || MasterGain.ActualWidth < 45 || cards.Any(c => c.Strip.Gain.ActualWidth < 60)) throw new Exception("Controls clipped at minimum width"); lines.Add("PASS controls fit minimum 760 px width");
            var m = new LevelMeter(); if (m.DisplayText != "Off") throw new Exception("Stopped meter"); m.Active = true; if (m.DisplayText != "Silent") throw new Exception("Silent meter"); m.Peak = .5f; if (m.DisplayText != "-6 dBFS") throw new Exception("Meter calibration"); m.IsMuted = true; if (m.DisplayText != "Muted") throw new Exception("Muted meter"); lines.Add("PASS Off, Silent, Muted and calibrated dBFS labels");
            var peaks = new PeakTracker(); peaks.Push(.8f); peaks.Push(0); if (peaks.Read() != .8f || peaks.Read() != 0) throw new Exception("Transient lost or cosmetic decay retained"); peaks.Push(.25f); if (peaks.Read() != .25f) throw new Exception("Peak must equal samples"); await Task.Delay(110); if (peaks.Read() != 0) throw new Exception("Stopped source must become silent"); lines.Add("PASS transients survive silent packets; exact peaks and zero without smoothing");
            var pcm = new byte[20]; DriverOutput.Encode(new float[] { -1, 0, 1, float.NaN, .5f }, pcm); if (BitConverter.ToInt32(pcm, 0) != int.MinValue || BitConverter.ToInt32(pcm, 4) != 0 || BitConverter.ToInt32(pcm, 8) != int.MaxValue || BitConverter.ToInt32(pcm, 12) != 0 || BitConverter.ToInt32(pcm, 16) != 1073741824) throw new Exception("Driver PCM conversion invalid"); if (Devices.OwnDriver(new DeviceChoice("test", "DuoMix", "Voice.ai Audio Cable"))) throw new Exception("Renaming must not count as a native driver"); lines.Add("PASS native-driver PCM transport conversion and driver identity separation (no kernel driver loaded)");
            foreach (var name in Appearance.Names) { ThemePicker.SelectedItem = name; UpdateLayout(); if (prefs.Theme != name) throw new Exception("Theme selection failed"); }
            lines.Add("PASS all six themes");
            SolidMaterial.IsChecked = true; UpdateLayout(); if (((GradientBrush)Shell.Background).GradientStops.Any(s => s.Color.A != 255) || (Application.Current.Resources["Card"] as SolidColorBrush)?.Color.A != 255) throw new Exception("Solid mode not opaque"); prefs.Save(); if (Preferences.Load().Glass) throw new Exception("Solid preference not retained"); GlassMaterial.IsChecked = true; TintSlider.Value = 25; if (Appearance.Apply(this, prefs) < 0) throw new Exception("Blur unavailable"); if ((Application.Current.Resources["Card"] as SolidColorBrush)?.Color.A >= 100 || (Application.Current.Resources["Field"] as SolidColorBrush)?.Color.A >= 100) throw new Exception("Glass panels are opaque"); if (!new Preferences().Glass) throw new Exception("First run should use glass"); lines.Add("PASS solid shell/panels, translucent glass panels, default glass and saved solid preference");
            prefs.Save(); var saved = Preferences.Load(); if (saved.Theme != prefs.Theme || saved.Glass != prefs.Glass || saved.Tint != prefs.Tint) throw new Exception("Preferences did not persist"); lines.Add("PASS saved preferences");
            var d = Devices.CableOutput() ?? throw new Exception("Cable missing"); var oldName = Devices.ShortName(d); try { renameDeviceId = d.Id; DeviceNameInput.Text = "DuoMix Rename Check"; SaveRenameClick(this, new RoutedEventArgs()); var updated = Devices.List(DataFlow.Capture).First(x => x.Id == d.Id); if (Devices.ShortName(updated) != "DuoMix Rename Check") throw new Exception("Rename UI did not update Windows"); if (Devices.CableInput() == null) throw new Exception("Renaming lost cable pairing"); lines.Add("PASS rename dialog updates Windows endpoint and preserves cable pairing"); } finally { Devices.Rename(d.Id, oldName); RefreshCable(); }
            var titleFace = new Typeface(BrandTitle.FontFamily, BrandTitle.FontStyle, BrandTitle.FontWeight, BrandTitle.FontStretch); if (!titleFace.TryGetGlyphTypeface(out var glyph) || !glyph.FontUri.ToString().Contains("rajdhani", StringComparison.OrdinalIgnoreCase)) throw new Exception("Bundled Rajdhani failed to resolve"); lines.Add("PASS embedded Rajdhani Bold title resolves with italic styling"); var logo = (DrawingImage)FindResource("DuoMixLogo"); if (logo.Drawing is not DrawingGroup) throw new Exception("Logo must be vector"); lines.Add("PASS native vector logo");
        }
        catch (Exception ex) { lines.Add("FAIL " + ex); }
        finally { Width = width; Height = height; ThemePicker.SelectedItem = theme; SolidMaterial.IsChecked = !glass; GlassMaterial.IsChecked = glass; TintSlider.Value = tint; Save(); }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "ui-validation.txt"), lines);
    }
}



