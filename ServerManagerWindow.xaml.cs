using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AfranHospitalKiosk;

public partial class ServerManagerWindow : Window
{
    private Process? _apiProcess;
    private Window? _kioskWindow;
    private Window? _doctorWindow;
    private Window? _tvWindow;

    public ServerManagerWindow()
    {
        InitializeComponent();
        KeyDown += Window_KeyDown;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void StartApiButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiProcess != null && !_apiProcess.HasExited)
        {
            StopApi();
            return;
        }

        try
        {
            // Try multiple path candidates to find QueueApi directory
            string[] pathCandidates = 
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "QueueApi"),
                System.IO.Path.Combine(AppContext.BaseDirectory, "QueueApi"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "QueueApi"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "QueueApi")
            };

            string? apiProjectPath = null;
            foreach (var candidate in pathCandidates)
            {
                if (System.IO.Directory.Exists(candidate))
                {
                    apiProjectPath = System.IO.Path.GetFullPath(candidate);
                    break;
                }
            }

            if (apiProjectPath == null)
            {
                MessageBox.Show(
                    $"QueueApi directory not found.\n\nSearched in:\n{string.Join("\n", pathCandidates.Select(p => $"- {System.IO.Path.GetFullPath(p)}"))}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{apiProjectPath}\"",
                WorkingDirectory = apiProjectPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _apiProcess = Process.Start(startInfo);
            
            if (_apiProcess != null)
            {
                _apiProcess.EnableRaisingEvents = true;
                _apiProcess.Exited += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ApiStatusText.Text = "Status: Stopped";
                        ApiStatusIndicator.Fill = System.Windows.Media.Brushes.Gray;
                        StartApiButton.Content = "Start API";
                        StartApiButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105));
                        _apiProcess = null;
                    });
                };

                ApiStatusText.Text = "Status: Running";
                ApiStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105));
                StartApiButton.Content = "Stop API";
                StartApiButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start API: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void StopApi()
    {
        if (_apiProcess != null && !_apiProcess.HasExited)
        {
            try
            {
                _apiProcess.Kill();
                _apiProcess.WaitForExit(5000);
            }
            catch
            {
                // Process may have already exited
            }

            ApiStatusText.Text = "Status: Stopped";
            ApiStatusIndicator.Fill = System.Windows.Media.Brushes.Gray;
            StartApiButton.Content = "Start API";
            StartApiButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105));
            _apiProcess = null;
        }
    }

    private void LaunchKioskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_kioskWindow != null)
        {
            _kioskWindow.Focus();
            return;
        }

        _kioskWindow = new MainWindow();
        _kioskWindow.Show();
        _kioskWindow.Closed += (s, args) =>
        {
            _kioskWindow = null;
            KioskStatusText.Text = "Not running";
            KioskStatusDot.Fill = System.Windows.Media.Brushes.Gray;
        };
        KioskStatusText.Text = "Running";
        KioskStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
    }

    private void LaunchDoctorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_doctorWindow != null)
        {
            _doctorWindow.Focus();
            return;
        }

        _doctorWindow = new DoctorWindow();
        _doctorWindow.Show();
        _doctorWindow.Closed += (s, args) =>
        {
            _doctorWindow = null;
            DoctorStatusText.Text = "Not running";
            DoctorStatusDot.Fill = System.Windows.Media.Brushes.Gray;
        };
        DoctorStatusText.Text = "Running";
        DoctorStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(124, 58, 237));
    }

    private void LaunchTvButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tvWindow != null)
        {
            _tvWindow.Focus();
            return;
        }

        _tvWindow = new TvWindow();
        _tvWindow.Show();
        _tvWindow.Closed += (s, args) =>
        {
            _tvWindow = null;
            TvStatusText.Text = "Not running";
            TvStatusDot.Fill = System.Windows.Media.Brushes.Gray;
        };
        TvStatusText.Text = "Running";
        TvStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6));
    }

    private void CloseAllButton_Click(object sender, RoutedEventArgs e)
    {
        _kioskWindow?.Close();
        _doctorWindow?.Close();
        _tvWindow?.Close();
        StopApi();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up when server manager closes
        _kioskWindow?.Close();
        _doctorWindow?.Close();
        _tvWindow?.Close();
        StopApi();
        base.OnClosed(e);
    }
}
