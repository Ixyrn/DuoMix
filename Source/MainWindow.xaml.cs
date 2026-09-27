using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;
using System.ComponentModel;
using NAudio.CoreAudioApi;
namespace DuoMix;
public partial class MainWindow : Window
{
    readonly Preferences prefs = Preferences.Load(); readonly List<ProgramCard> cards = []; readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    List<AppChoice> apps = []; MixerEngine? engine; bool loaded, busy, closing; bool closePending;
    public MainWindow()
    {
        InitializeComponent(); Height = Math.Min(710, SystemParameters.WorkArea.Height - 30); Width = Math.Min(820, SystemParameters.WorkArea.Width - 30);
        ThemePicker.ItemsSource = Appearance.Names; ThemePicker.SelectedItem = prefs.Theme; SolidMaterial.IsChecked = !prefs.Glass; GlassMaterial.IsChecked = prefs.Glass; TintSlider.Value = prefs.Tint; MicStrip.Gain.Value = prefs.MicGain; MicStrip.Mute.IsChecked = prefs.MicMuted; MasterGain.Value = prefs.MasterGain;
        MicStrip.Changed += () => { prefs.MicGain = MicStrip.Gain.Value; prefs.MicMuted = MicStrip.Mute.IsChecked == true; Save(); }; RefreshDevices(); loaded = true;
        SourceInitialized += (_, _) => ApplyAppearance(); Loaded += async (_, _) => { ApplyAppearance(); await SyncMonitors(); }; StateChanged += (_, _) => UpdateCaption(); Closing += OnClosing;
        timer.Tick += (_, _) => { MicStrip.Tick(); foreach (var c in cards) c.Strip.Tick(); OutputMeter.Active = engine != null; OutputMeter.Peak = engine?.Master.Peaks.Read() ?? 0; OutputMeter.InvalidateVisual(); }; timer.Start(); PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) HideDialog(); };
    }
    public void SetShellBrush(Brush brush) => Shell.Background = brush;
    void Save() { if (!loaded) return; try { prefs.Save(); } catch (Exception ex) { Status.Text = "Preferences could not be saved: " + ex.Message; } }
    void ApplyAppearance() { int hr = Appearance.Apply(this, prefs); TintSlider.IsEnabled = prefs.Glass; GlassHint.Text = prefs.Glass && hr < 0 ? "Desktop blur is unavailable on this Windows version. Using an opaque fallback." : "Tint changes the transparency of the window and panels. Solid Color is fully opaque."; OutputMeter.InvalidateVisual(); MicStrip.Meter.InvalidateVisual(); }
    void AppearanceChanged(object sender, SelectionChangedEventArgs e) { if (!loaded) return; prefs.Theme = ThemePicker.SelectedItem as string ?? Appearance.Names[0]; ApplyAppearance(); Save(); }
    void MaterialChanged(object sender, RoutedEventArgs e) { if (!loaded) return; prefs.Glass = GlassMaterial.IsChecked == true; ApplyAppearance(); Save(); }
    void TintChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!loaded) return; prefs.Tint = TintSlider.Value; ApplyAppearance(); Save(); }
    async void MicChanged(object sender, SelectionChangedEventArgs e) { if (!loaded) return; prefs.Microphone = (MicPicker.SelectedItem as DeviceChoice)?.Id; Save(); await SyncMonitors(); }
    void MasterChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (MasterValue == null) return; MasterValue.Text = $"{MasterGain.Value:0}%"; if (engine != null) engine.Master.Gain = (float)(MasterGain.Value / 100 * 0.8); prefs.MasterGain = MasterGain.Value; Save(); }
    void RefreshDevices() { try { string? id = prefs.Microphone; bool prior = loaded; loaded = false; try { if (engine == null) { var list = Devices.List(DataFlow.Capture).Where(d => !Devices.Cable(d) && d.Id != prefs.CableCaptureId).ToList(); MicPicker.ItemsSource = list; MicPicker.SelectedItem = list.FirstOrDefault(d => d.Id == id) ?? list.FirstOrDefault(d => d.Name.Contains("Focusrite")) ?? list.FirstOrDefault(); } apps = Devices.Apps(); RefreshCable(); } finally { loaded = prior; } } catch (Exception ex) { Report(ex); } }
    void RefreshCable() { var output = CurrentRoute()?.Output; CableName.Text = output == null ? "Choose a virtual cable connection" : Devices.ShortName(output); DriverInfo.Text = output == null ? "No virtual cable connected. Use Connection on the game microphone card." : "Virtual cable: " + output.Adapter + ". Change it with Connection on the game microphone card."; ReadyLabel.Text = output == null ? "Setup needed" : engine == null ? "Ready" : "Live"; }
    async void RefreshClick(object sender, RoutedEventArgs e) { if (busy) return; RefreshDevices(); Status.Text = "Devices and running programs refreshed."; await SyncMonitors(); }
    void SettingsClick(object sender, RoutedEventArgs e) { RoutingPanel.Visibility = Visibility.Collapsed; RenamePanel.Visibility = Visibility.Collapsed; DialogTitle.Text = "Settings"; SettingsPanel.Visibility = Visibility.Visible; PickerPanel.Visibility = Visibility.Collapsed; ShowDialogPanel(); ThemePicker.Focus(); }
    void DismissClick(object sender, RoutedEventArgs e) => HideDialog();
    void AddProgramClick(object sender, RoutedEventArgs e) { if (busy) return; apps = Devices.Apps(); RoutingPanel.Visibility = Visibility.Collapsed; RenamePanel.Visibility = Visibility.Collapsed; DialogTitle.Text = "Add a program"; SettingsPanel.Visibility = Visibility.Collapsed; PickerPanel.Visibility = Visibility.Visible; ShowDialogPanel(); AppSearch.Text = ""; FilterApps(); AppSearch.Focus(); }
    void SearchChanged(object sender, TextChangedEventArgs e) { if (AppList != null) FilterApps(); }
    void FilterApps() { AppList.ItemsSource = apps.Where(a => !cards.Any(c => c.App.Pid == a.Pid) && a.Name.Contains(AppSearch.Text, StringComparison.OrdinalIgnoreCase)).ToList(); if (AppList.Items.Count > 0) AppList.SelectedIndex = 0; }
    async void ConfirmAddClick(object sender, RoutedEventArgs e) => await AddSelected(); async void AppDoubleClick(object sender, MouseButtonEventArgs e) => await AddSelected();
    async Task AddSelected() { if (busy || AppList.SelectedItem is not AppChoice app) return; await AddApp(app); }
    public async Task AddApp(AppChoice app) { if (busy || cards.Any(c => c.App.Pid == app.Pid)) return; busy = true; ProgramCard? card = null; try { card = new(app); if (engine != null) { card.Strip.Source = await engine.AddApp(app.Pid); card.Strip.Apply(); } card.RemoveRequested += RemoveApp; cards.Add(card); ProgramRows.Children.Add(card); Motion.Pop(card); EmptyPrograms.Visibility = Visibility.Collapsed; HideDialog(); Status.Text = $"Added {app.Name}."; await SyncMonitors(); } catch (Exception ex) { Report(ex); } finally { busy = false; FinishPendingClose(); } }
    async Task RemoveApp(ProgramCard card) { if (busy) return; busy = true; try { if (engine != null && card.Strip.Source != null) await engine.Remove(card.Strip.Source); cards.Remove(card); ProgramRows.Children.Remove(card); EmptyPrograms.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed; await SyncMonitors(); } catch (Exception ex) { await Stop(); Report(ex); } finally { busy = false; FinishPendingClose(); } }
    async void MixClick(object sender, RoutedEventArgs e) { if (busy) return; busy = true; MixButton.IsEnabled = false; try { if (engine == null) await Start(); else await Stop(); } catch (Exception ex) { await Stop(); Report(ex); } finally { busy = false; MixButton.IsEnabled = true; FinishPendingClose(); } }
    async Task Start() { if (MicPicker.SelectedItem is not DeviceChoice mic) throw new Exception("Select an available microphone."); var route = CurrentRoute() ?? throw new Exception("Choose an available virtual cable using Connection, or install VB-CABLE and refresh."); var output = route.Input; mixingRoute = route; suspendMonitoring = true; MicPicker.IsEnabled = false; await SyncMonitors(); var next = new MixerEngine(); engine = next; next.Failed += message => Dispatcher.BeginInvoke(async () => { if (engine != next) return; await Stop(); Status.Text = "Audio stopped: " + message; }); MicStrip.Source = await next.AddMic(mic.Id); MicStrip.Apply(); foreach (var card in cards) { card.Strip.Source = await next.AddApp(card.App.Pid); card.Strip.Apply(); } next.Master.Gain = (float)(MasterGain.Value / 100 * 0.8); next.Start(output.Id); MicPicker.IsEnabled = false; MixButton.Content = "■  Stop Mixing"; MixButton.Background = new SolidColorBrush(Color.FromArgb(50, 255, 75, 98)); MixButton.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 75, 98)); MixButton.Foreground = new SolidColorBrush(Color.FromRgb(255, 93, 110)); Status.Text = "Live  •  mic + selected programs → game microphone"; RefreshCable(); }
    async Task Stop() { var old = engine; engine = null; try { if (old != null) await old.DisposeAsync(); } finally { MicStrip.Source = null; foreach (var c in cards) c.Strip.Source = null; MicPicker.IsEnabled = true; MixButton.Content = "▶  Start Mixing"; MixButton.SetResourceReference(BackgroundProperty, "Accent"); MixButton.SetResourceReference(BorderBrushProperty, "Accent"); MixButton.Foreground = Brushes.White; Status.Text = "Monitoring sources • game microphone is off"; RefreshCable(); suspendMonitoring = shutdown; await SyncMonitors(); } }
    async void OnClosing(object? sender, CancelEventArgs e) { if (closing) return; e.Cancel = true; if (busy) { closePending = true; return; } busy = true; shutdown = true; timer.Stop(); try { await Stop(); } finally { closing = true; _ = Dispatcher.BeginInvoke(Close); } }
    void FinishPendingClose() { if (closePending) { closePending = false; Close(); } }
    string? renameDeviceId;
    void RenameClick(object sender, RoutedEventArgs e) { try { var d = CurrentRoute()?.Output ?? throw new Exception("Choose a virtual cable connection first."); RoutingPanel.Visibility = Visibility.Collapsed; renameDeviceId = d.Id; DeviceNameInput.Text = Devices.ShortName(d); RenameError.Text = ""; DialogTitle.Text = "Rename device"; SettingsPanel.Visibility = Visibility.Collapsed; PickerPanel.Visibility = Visibility.Collapsed; RenamePanel.Visibility = Visibility.Visible; ShowDialogPanel(); DeviceNameInput.Focus(); DeviceNameInput.SelectAll(); } catch (Exception ex) { Report(ex); } }
    void RenameKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { SaveRenameClick(sender, e); e.Handled = true; } }
    void SaveRenameClick(object sender, RoutedEventArgs e) { try { if (renameDeviceId == null) throw new Exception("Choose a device first."); string name = DeviceNameInput.Text.Trim(); Devices.Rename(renameDeviceId, name); var updated = Devices.List(DataFlow.Capture).First(d => d.Id == renameDeviceId); if (Devices.ShortName(updated) != name) throw new Exception("Windows did not confirm the new name. Please try again."); RefreshCable(); Status.Text = $"Device renamed to {name}. Refresh your game's microphone list."; HideDialog(); } catch (Exception ex) { RenameError.Text = ex.Message; } }
    int dialogGeneration;
    void ShowDialogPanel() { Dialog.MaxHeight = Math.Max(280, Math.Min(610, ActualHeight - 40)); dialogGeneration++; Overlay.BeginAnimation(OpacityProperty, null); Overlay.Opacity = 1; Overlay.Visibility = Visibility.Visible; Motion.Pop(Dialog); }
    async void HideDialog() { if (Overlay.Visibility != Visibility.Visible) return; int generation = ++dialogGeneration; await Motion.FadeOut(Overlay); if (generation == dialogGeneration) { Overlay.Visibility = Visibility.Collapsed; Overlay.BeginAnimation(OpacityProperty, null); Overlay.Opacity = 1; } }
    void DeviceSettingsClick(object sender, RoutedEventArgs e) { try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); } catch (Exception ex) { Report(ex); } }
    void Report(Exception ex) { Status.Text = ex.Message; System.Windows.MessageBox.Show(this, ex.Message, "DuoMix", MessageBoxButton.OK, MessageBoxImage.Warning); }
    void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void UpdateCaption() { bool restored = WindowState == WindowState.Maximized; MaximizeGlyph.Data = Geometry.Parse(restored ? "M3.5,0.5 L12.5,0.5 L12.5,9.5 M0.5,3.5 L9.5,3.5 L9.5,12.5 L0.5,12.5 Z" : "M0.5,0.5 L12.5,0.5 L12.5,12.5 L0.5,12.5 Z"); MaximizeButton.ToolTip = restored ? "Restore down" : "Maximize"; System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, restored ? "Restore down" : "Maximize"); }
    void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void CloseClick(object sender, RoutedEventArgs e) => Close();
    public void Snapshot(string path) { UpdateLayout(); var bmp = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(this); var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = System.IO.File.Create(path); enc.Save(f); }
    public async Task Preview() { var app = apps.FirstOrDefault(a => a.Name.Contains("Spotify", StringComparison.OrdinalIgnoreCase)); if (app != null) await AddApp(app); }
}






