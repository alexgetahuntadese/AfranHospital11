using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AfranHospitalKiosk;

public partial class TvWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _fallbackTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly QueueApiClient _apiClient = new();
    private readonly AmharicTicketAnnouncer _announcer = new();
    private int _fallbackTicket = 105;

    public TvWindow()
    {
        InitializeComponent();

        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        Loaded += (_, _) =>
        {
            ApplyDisplayScale();
            StartCorridorVideo();
        };
        SizeChanged += (_, _) => ApplyDisplayScale();

        _ = ConnectApiAsync();
    }

    private void ApplyDisplayScale()
    {
        var largeTv = ActualWidth >= 1800 || ActualHeight >= 1000;

        WaitingStripRow.Height = new GridLength(largeTv ? 170 : 132);
        WaitingStrip.Padding = largeTv ? new Thickness(22) : new Thickness(18);
        NextPatientsTitle.FontSize = largeTv ? 36 : 28;
        NextPatientsTitle.Margin = largeTv ? new Thickness(8, 0, 30, 0) : new Thickness(8, 0, 24, 0);

        foreach (var ticket in new[] { QueueTicket1, QueueTicket2, QueueTicket3, QueueTicket4, QueueTicket5, QueueTicket6 })
        {
            ticket.FontSize = largeTv ? 48 : 34;
        }
    }

    private void StartCorridorVideo()
    {
        var videoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AddisSlideshow", "corridor-development-video.mp4");
        if (!File.Exists(videoPath))
        {
            return;
        }

        CorridorVideo.Source = new Uri(videoPath, UriKind.Absolute);
        CorridorVideo.Position = TimeSpan.Zero;
        CorridorVideo.Play();
    }

    private void CorridorVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        CorridorVideo.Position = TimeSpan.Zero;
        CorridorVideo.Play();
    }

    private async Task ConnectApiAsync()
    {
        try
        {
            await RefreshDisplayAsync();
            await _apiClient.ConnectHubAsync(display =>
            {
                Dispatcher.Invoke(() => ApplyDisplay(display));
            }, ticket =>
            {
                Dispatcher.Invoke(() =>
                {
                    NowServingLabel.Text = ticket.Ticket;
                    RoomTicket3.Text = ticket.Ticket;
                    RoomDisplayLabel.Text = ticket.RoomNumber is not null
                        ? $"GO TO ROOM {ticket.RoomNumber}"
                        : $"GO TO ROOM {RoomFor(ticket)}";
                });
                _ = _announcer.AnnounceAsync(ticket.Ticket, ticket.Language, ticket.RoomNumber);
            });
        }
        catch (Exception ex)
        {
            TickerLabel.Text = "Queue API offline  •  Demo display mode";
            _fallbackTimer.Tick += (_, _) => RotateFallbackTicket();
            _fallbackTimer.Start();
            RotateFallbackTicket();
            System.Diagnostics.Debug.WriteLine($"TV API Connection Error: {ex.Message}");
        }
    }

    private async Task RefreshDisplayAsync()
    {
        var display = await _apiClient.GetDisplayAsync();
        if (display is not null)
        {
            ApplyDisplay(display);
        }
    }

    private void ApplyDisplay(QueueDisplay display)
    {
        var nowServing = display.NowServing?.Ticket ?? "-";
        NowServingLabel.Text = nowServing;
        RoomTicket3.Text = nowServing;
        RoomDisplayLabel.Text = display.NowServing?.RoomNumber is not null
            ? $"GO TO ROOM {display.NowServing.RoomNumber}"
            : $"GO TO ROOM {RoomFor(display.NowServing)}";

        SetRoomTickets(display);
        SetQueueRows(display.Waiting);

        TickerLabel.Text = $"Waiting: {display.WaitingCount}  •  HIWOT FANA INTERNAL MEDICINE SPECIALTY CLINIC  •  እንኳን ደህና መጡ  •  Baga nagaan dhuftan";
    }

    private void SetRoomTickets(QueueDisplay display)
    {
        SetText(RoomTicket1, display.Waiting.ElementAtOrDefault(1)?.Ticket);
        SetText(RoomTicket2, display.Waiting.ElementAtOrDefault(2)?.Ticket);
        SetText(RoomTicket4, display.Waiting.ElementAtOrDefault(3)?.Ticket);
        SetText(RoomTicket5, display.Waiting.ElementAtOrDefault(4)?.Ticket);
    }

    private void SetQueueRows(IReadOnlyList<TicketDto> waiting)
    {
        SetQueueRow(QueueTicket1, QueueGender1, waiting.ElementAtOrDefault(0));
        SetQueueRow(QueueTicket2, QueueGender2, waiting.ElementAtOrDefault(1));
        SetQueueRow(QueueTicket3, QueueGender3, waiting.ElementAtOrDefault(2));
        SetQueueRow(QueueTicket4, QueueGender4, waiting.ElementAtOrDefault(3));
        SetQueueRow(QueueTicket5, QueueGender5, waiting.ElementAtOrDefault(4));
        SetQueueRow(QueueTicket6, QueueGender6, waiting.ElementAtOrDefault(5));
    }

    private static void SetQueueRow(TextBlock ticketLabel, TextBlock genderLabel, TicketDto? ticket)
    {
        ticketLabel.Text = ticket?.Ticket ?? "-";
        var isFemale = ticket?.Gender.Equals("Female", StringComparison.OrdinalIgnoreCase) == true;
        genderLabel.Text = isFemale ? "♀" : "♂";
        genderLabel.Foreground = new SolidColorBrush(isFemale ? Color.FromRgb(255, 91, 147) : Color.FromRgb(0, 183, 241));
    }

    private static string RoomFor(TicketDto? ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket?.RoomNumber))
        {
            return ticket.RoomNumber;
        }

        if (ticket?.Gender.Equals("Male", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "101";
        }

        return ticket?.Gender.Equals("Female", StringComparison.OrdinalIgnoreCase) == true
            ? "102"
            : "-";
    }

    private static void SetText(TextBlock label, string? value)
    {
        label.Text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockLabel.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        DateLabel.Text = now.ToString("dddd, MMM dd, yyyy", CultureInfo.CurrentCulture);
    }

    private void RotateFallbackTicket()
    {
        _fallbackTicket++;
        NowServingLabel.Text = $"M{_fallbackTicket:000}";
        RoomDisplayLabel.Text = "GO TO ROOM 101";
        RoomTicket1.Text = $"F{_fallbackTicket + 2:000}";
        RoomTicket2.Text = $"M{_fallbackTicket + 3:000}";
        RoomTicket3.Text = $"M{_fallbackTicket:000}";
        RoomTicket4.Text = $"F{_fallbackTicket + 4:000}";
        RoomTicket5.Text = $"M{_fallbackTicket + 5:000}";
        SetQueueRows(new[]
        {
            new TicketDto($"M{_fallbackTicket + 1:000}", "Male", "English", "Waiting", DateTime.Now),
            new TicketDto($"F{_fallbackTicket + 2:000}", "Female", "Amharic", "Waiting", DateTime.Now),
            new TicketDto($"M{_fallbackTicket + 3:000}", "Male", "English", "Waiting", DateTime.Now),
            new TicketDto($"F{_fallbackTicket + 4:000}", "Female", "English", "Waiting", DateTime.Now),
            new TicketDto($"M{_fallbackTicket + 5:000}", "Male", "Amharic", "Waiting", DateTime.Now),
            new TicketDto($"F{_fallbackTicket + 6:000}", "Female", "English", "Waiting", DateTime.Now)
        });
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _apiClient.DisposeAsync();
        base.OnClosed(e);
    }
}
