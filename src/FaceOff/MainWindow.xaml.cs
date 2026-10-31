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
                    SetHeadline("ON", "AccentColor");
                    SetToggle("TURN OFF", primary: true);
                    break;
                case AntiCheatState.OffAfterRestart:
                    SetHeadline("OFF", "WarnColor");
                    SetToggle("TURN ON", primary: false);
                    break;
                case AntiCheatState.Off:
                    SetHeadline("OFF", "OkColor");
                    SetToggle("TURN ON", primary: false);
                    break;
                default:
                    SetHeadline("NOT FOUND", "MutedColor");
                    SetToggle("TURN OFF", primary: true);
                    ToggleButton.IsEnabled = false;
                    break;
            }

            TargetRows.ItemsSource = new[]
            {
                Row("Driver", status.Driver),
                Row("Service", status.Service),
            };

            bool pendingRestart = _state == AntiCheatState.OffAfterRestart;
            if (pendingRestart) RestartText.Text = "Restart to finish turning off.";
            RestartBanner.Visibility = pendingRestart || RestartBanner.Tag != null ? Visibility.Visible : Visibility.Collapsed;
        }

        object Row(string title, ServiceInfo s)
        {
            if (!s.Installed)
                return new { Title = title, RunState = "Not installed", DotBrush = FindResource("Muted") };
            return new
            {
                Title = title,
                RunState = s.Running ? "Running" : "Stopped",
                DotBrush = FindResource(s.Running ? "Accent" : "Ok"),
            };
        }

        void SetHeadline(string text, string colorKey)
        {
            Headline.Text = text;
            var color = (Color)FindResource(colorKey);
            HeadlineFg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(200)));
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
                if (RestartBanner.Tag != null) RestartText.Text = "Restart before you play.";
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
                RestartButton.Content = "CLICK TO CONFIRM";
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
            RestartButton.Content = "RESTART";
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
            DetailsButton.Content = show ? "HIDE DETAILS" : "DETAILS";
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
