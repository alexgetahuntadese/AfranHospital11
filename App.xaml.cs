using System.Windows;

namespace AfranHospitalKiosk;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var mode = e.Args.FirstOrDefault()?.Trim().ToLowerInvariant();
        Window window = mode switch
        {
            "doctor" or "dr" => new DoctorWindow(),
            "tv" or "display" => new TvWindow(),
            _ => new MainWindow()
        };

        window.Show();
    }
}
