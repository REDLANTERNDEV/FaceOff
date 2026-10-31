using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FaceOff
{
    public partial class MainWindow : Window
    {
        readonly StateStore _store;
        readonly Toggler _toggler;
        AntiCheatState _state;
        bool _restartArmed;
        readonly DispatcherTimer _disarmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };

        public MainWindow()
        {
            InitializeComponent();
            _store = StateStore.Default();
            _toggler = new Toggler(_store);
            KeepAppCheck.IsChecked = _store.KeepApp;
            var v =Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{v.Major}.{v.Minor}";
            _disarmTimer.Tick += (s, e) => DisarmRestart();
            SourceInitialized += (s, e) => ApplyDarkFrame();
            Loaded += async (s, e) => await RefreshAsync();
        }

        async Task RefreshAsync()
        {
            try { Render(await Task.Run(() => _toggler.GetStatus())); }
            catch (Exception e) { ShowError(e.Message); }
        }

        void Render(Status status)
        {
            _state = status.State;
            switch (_state)
            {
                case AntiCheatState.On:
                    SetPill("ON", (Color)FindResource("AccentColor"));
                    Headline.Text = "FACEIT is enabled";
                    Subline.Text = "The kernel driver can load with Windows. Turn it off when you're done playing.";
                    SetToggle("TURN OFF", primary: true);
                    break;
                case AntiCheatState.OffAfterRestart:
                    SetPill("OFF AFTER RESTART", (Color)FindResource("WarnColor"));
                    Headline.Text = "Disabled, still loaded";
                    Subline.Text = "FACEIT won't start again, but the driver stays in memory until Windows restarts.";
                    SetToggle("TURN ON", primary: false);
                    break;
                case AntiCheatState.Off:
                    SetPill("OFF", (Color)FindResource("OkColor"));
                    Headline.Text = "FACEIT is off";
                    Subline.Text = "Nothing from FACEIT loads at boot. Turn it on before you play.";
                    SetToggle("TURN ON", primary: false);
                    break;
                default:
                    SetPill("NOT FOUND", (Color)FindResource("MutedColor"));
                    Headline.Text = "FACEIT not found";
                    Subline.Text = "The FACEIT anti-cheat driver and service aren't installed on this PC.";
                    SetToggle("NOTHING TO DO", primary: true);
                    ToggleButton.IsEnabled = false;
                    break;
            }

            KeepAppCheck.Visibility = _state == AntiCheatState.On ? Visibility.Visible : Visibility.Collapsed;

            TargetRows.ItemsSource = new[]
            {
                Row("Kernel driver", status.Driver, "FACEIT_AC.sys"),
                Row("Anti-cheat service", status.Service, Targets.Service),
            };

            bool pendingRestart = _state == AntiCheatState.OffAfterRestart;
            if (pendingRestart) RestartText.Text = "Driver is still loaded. Restart Windows to unload it.";
            RestartBanner.Visibility = pendingRestart || RestartBanner.Tag != null ? Visibility.Visible : Visibility.Collapsed;
        }

        object Row(string title, ServiceInfo s, string displayName)
        {
            if (!s.Installed)
                return new { Title = title, Name = displayName, RunState = "Not installed", StartText = "", DotBrush = FindResource("Muted") };
            return new
            {
                Title = title,
                Name = displayName,
                RunState = s.Running ? "Running" : "Stopped",
                StartText = "Start: " + Friendly(s.StartType),
                DotBrush = FindResource(s.Running ? "Accent" : "Ok"),
            };
        }

        static string Friendly(StartType t)
        {
            switch (t)
            {
                case StartType.Boot: return "Boot";
                case StartType.System: return "With Windows";
                case StartType.Auto: return "Automatic";
                case StartType.Demand: return "Manual";
                default: return "Disabled";
            }
        }

        void SetPill(string text, Color color)
        {
            PillText.Text = text;
            var fade = TimeSpan.FromMilliseconds(200);
            PillDot.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, fade));
            PillFg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, fade));
            PillBg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Color.FromArgb(0x26, color.R, color.G, color.B), fade));
        }

        void SetToggle(string text, bool primary)
        {
            ToggleText.Text = text;
            ToggleButton.Style = (Style)FindResource(primary ? "PrimaryButton" : "OutlineButton");
            ToggleButton.IsEnabled = true;
        }

        async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            bool turningOff = _state == AntiCheatState.On;
            bool keepApp = KeepAppCheck.IsChecked == true;
            ErrorText.Visibility = Visibility.Collapsed;
            SetBusy(true, turningOff ? "TURNING OFF" : "TURNING ON");
            try
            {
                var result = await Task.Run(() => turningOff ? _toggler.Disable(keepApp) : _toggler.Enable());
                DetailsText.Text = string.Join(Environment.NewLine, result.Log);
                DetailsButton.Visibility = Visibility.Visible;

                // Enable can also need a restart (driver that loads at boot); keep the banner up for that case.
                RestartBanner.Tag = !turningOff && result.RebootRequired ? (object)true : null;
                if (RestartBanner.Tag != null) RestartText.Text = "FACEIT's driver loads at boot. Restart Windows before you play.";
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                await RefreshAsync();
            }
        }

        void SetBusy(bool busy, string text)
        {
            ToggleButton.IsEnabled = !busy;
            Spinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (busy)
            {
                ToggleText.Text = text;
                SpinnerRotation.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            else SpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        // Two-step confirm so a stray click never reboots the PC.
        void Restart_Click(object sender, RoutedEventArgs e)
        {
            if (!_restartArmed)
            {
                _restartArmed = true;
                RestartButton.Content = "CLICK AGAIN TO RESTART";
                _disarmTimer.Start();
                return;
            }
            _disarmTimer.Stop();
            try
            {
                Toggler.Reboot();
                RestartButton.Content = "RESTARTING…";
                RestartButton.IsEnabled = false;
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        void DisarmRestart()
        {
            _disarmTimer.Stop();
            _restartArmed = false;
            RestartButton.Content = "RESTART NOW";
        }

        void KeepApp_Changed(object sender, RoutedEventArgs e)
        {
            if (_store == null) return; // fired during InitializeComponent
            _store.KeepApp = KeepAppCheck.IsChecked == true;
            try { _store.Save(); }
            catch (Exception ex) { ShowError("Could not save setting: " + ex.Message); }
        }

        void Details_Click(object sender, RoutedEventArgs e)
        {
            bool show = DetailsPanel.Visibility != Visibility.Visible;
            DetailsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DetailsButton.Content = show ? "HIDE DETAILS" : "SHOW DETAILS";
        }

        void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void Close_Click(object sender, RoutedEventArgs e) => Close();

        // Dark native frame + Windows 11 rounded corners and border. Ignored on older builds.
        void ApplyDarkFrame()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1, round = DWMWCP_ROUND, border = 0x002F2F2F; // COLORREF is 0x00BBGGRR
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34;
        const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
