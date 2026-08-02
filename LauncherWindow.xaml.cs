using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AfranHospitalKiosk;

public partial class LauncherWindow : Window
{
    private readonly List<Window> _moduleWindows = [];
    private readonly DispatcherTimer _healthTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private Process? _apiProcess;

    public LauncherWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        _healthTimer.Tick += async (_, _) => await RefreshApiStatusAsync();
        Loaded += async (_, _) => await RefreshApiStatusAsync();
        _healthTimer.Start();
    }

    private async void ApiButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiProcess is { HasExited: false }) StopApi();
        else await StartApiAsync();
    }

    private async Task StartApiAsync()
    {
        var clientUrl = NormalizeClientUrl(ApiUrlBox.Text);
        Environment.SetEnvironmentVariable("AFRAN_QUEUE_API", clientUrl);

        try
        {
            var apiDirectory = FindApiDirectory();
            var apiExe = Path.Combine(apiDirectory, "QueueApi.exe");
            ProcessStartInfo startInfo;
            if (File.Exists(apiExe))
            {
                startInfo = new ProcessStartInfo(apiExe)
                {
                    WorkingDirectory = apiDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.Environment["ASPNETCORE_URLS"] = "http://0.0.0.0:5000";
            }
            else
            {
                var project = Path.Combine(apiDirectory, "QueueApi.csproj");
                if (!File.Exists(project)) throw new FileNotFoundException("Published QueueApi.exe was not found.", apiExe);
                startInfo = new ProcessStartInfo("dotnet", $"run --project \"{project}\"")
                {
                    WorkingDirectory = apiDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            _apiProcess = Process.Start(startInfo);
            if (_apiProcess is null) throw new InvalidOperationException("The Queue API process could not be started.");
            _apiProcess.EnableRaisingEvents = true;
            _apiProcess.Exited += (_, _) => Dispatcher.Invoke(UpdateApiStopped);
            ApiButton.Content = "Stop API";
            await WaitForApiAsync(clientUrl);
        }
        catch (Exception ex)
        {
            UpdateApiStopped();
            MessageBox.Show($"Could not start the Queue API.\n\n{ex.Message}\n\nPublish the deployment package first, or run the API project from the repository.", "Queue API", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task WaitForApiAsync(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (await IsApiHealthyAsync(client, url)) { SetApiStatus(true, "Running · " + url); return; }
            await Task.Delay(500);
        }
        SetApiStatus(false, "Started, waiting for response · " + url);
    }

    private async Task RefreshApiStatusAsync()
    {
        var url = NormalizeClientUrl(ApiUrlBox.Text);
        Environment.SetEnvironmentVariable("AFRAN_QUEUE_API", url);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var healthy = await IsApiHealthyAsync(client, url);
        SetApiStatus(healthy, healthy ? "Online · " + url : (_apiProcess is { HasExited: false } ? "Starting · " + url : "Offline · " + url));
    }

    private static async Task<bool> IsApiHealthyAsync(HttpClient client, string url)
    {
        try { return (await client.GetAsync(url + "/")).IsSuccessStatusCode; }
        catch { return false; }
    }

    private void LaunchKiosk_Click(object sender, RoutedEventArgs e) => LaunchModule(() => new MainWindow(), KioskStatus, "Kiosk running");
    private void LaunchDoctor_Click(object sender, RoutedEventArgs e) => LaunchModule(() => new DoctorWindow(), DoctorStatus, "Doctor running");
    private void LaunchTv_Click(object sender, RoutedEventArgs e) => LaunchModule(() => new TvWindow(), TvStatus, "TV running");

    private void LaunchModule(Func<Window> factory, System.Windows.Controls.TextBlock status, string runningText)
    {
        var window = factory();
        _moduleWindows.Add(window);
        status.Text = runningText;
        window.Closed += (_, _) => { _moduleWindows.Remove(window); status.Text = "Stopped"; };
        window.Show();
    }

    private async void StartAll_Click(object sender, RoutedEventArgs e)
    {
        if (_apiProcess is not { HasExited: false }) await StartApiAsync();
        LaunchIfNotOpen(() => LaunchKiosk_Click(sender, e), KioskStatus);
        LaunchIfNotOpen(() => LaunchDoctor_Click(sender, e), DoctorStatus);
        LaunchIfNotOpen(() => LaunchTv_Click(sender, e), TvStatus);
    }

    private void LaunchIfNotOpen(Action launch, System.Windows.Controls.TextBlock status)
    {
        if (!_moduleWindows.Any(window => window.IsVisible && window.GetType() == (status == KioskStatus ? typeof(MainWindow) : status == DoctorStatus ? typeof(DoctorWindow) : typeof(TvWindow)))) launch();
    }

    private void StopAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var window in _moduleWindows.ToArray()) window.Close();
        StopApi();
    }

    private void StopApi()
    {
        try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(true); } catch { }
        _apiProcess = null;
        UpdateApiStopped();
    }

    private void UpdateApiStopped() => SetApiStatus(false, "Offline · " + NormalizeClientUrl(ApiUrlBox.Text));

    private void SetApiStatus(bool online, string text)
    {
        ApiStatusText.Text = text;
        ApiStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(online ? "#059669" : "#94A3B8"));
        if (_apiProcess is not { HasExited: false }) ApiButton.Content = "Start API";
    }

    private static string NormalizeClientUrl(string? value)
    {
        var url = string.IsNullOrWhiteSpace(value) ? "http://localhost:5000" : value.Trim().TrimEnd('/');
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? url : "http://" + url;
    }

    private static string FindApiDirectory()
    {
        var apiFolder = Environment.Is64BitOperatingSystem ? "QueueApi" : "QueueApi-x86";
        var candidates = new[] { Path.Combine(AppContext.BaseDirectory, "..", apiFolder), Path.Combine(AppContext.BaseDirectory, apiFolder), Path.Combine(Environment.CurrentDirectory, apiFolder), Path.Combine(AppContext.BaseDirectory, "..", "QueueApi") };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(Directory.Exists) ?? throw new DirectoryNotFoundException("QueueApi directory was not found next to the launcher.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _healthTimer.Stop();
        foreach (var window in _moduleWindows.ToArray()) window.Close();
        StopApi();
        base.OnClosed(e);
    }
}
