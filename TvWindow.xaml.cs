using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AfranHospitalKiosk;

public partial class TvWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _fallbackTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly DispatcherTimer _slideshowTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly QueueApiClient _apiClient = new();
    private readonly string[] _slides =
    [
        "Assets/AddisSlideshow/addis-01.jpg",
        "Assets/AddisSlideshow/addis-02.jpg",
        "Assets/AddisSlideshow/addis-03.tif",
        "Assets/AddisSlideshow/addis-04.jpg"
    ];
    private int _fallbackTicket = 105;
    private int _slideIndex;

    public TvWindow()
    {
        InitializeComponent();

        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        ShowSlide(0);
        _slideshowTimer.Tick += (_, _) => AdvanceSlide();
        _slideshowTimer.Start();

        _ = ConnectApiAsync();
    }

    private void AdvanceSlide()
    {
        _slideIndex = (_slideIndex + 1) % _slides.Length;
        ShowSlide(_slideIndex);
    }

    private void ShowSlide(int index)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(_slides[index], UriKind.Relative);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.EndInit();
        image.Freeze();
        SlideImage.Source = image;
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
                });
            });
        }
        catch
        {
            TickerLabel.Text = "Queue API offline  •  Demo display mode";
            _fallbackTimer.Tick += (_, _) => RotateFallbackTicket();
            _fallbackTimer.Start();
            RotateFallbackTicket();
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

        SetText(NextTicket1, display.Waiting.ElementAtOrDefault(0)?.Ticket);
        SetRoomTickets(display);
        SetQueueRows(display.Waiting);

        TickerLabel.Text = $"Waiting: {display.WaitingCount}  •  Welcome to Afran General Hospital  •  እንኳን ደህና መጡ  •  Baga nagaan dhuftan";
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
        NextTicket1.Text = $"M{_fallbackTicket + 1:000}";
        RoomTicket1.Text = $"F{_fallbackTicket + 2:000}";
        RoomTicket2.Text = $"M{_fallbackTicket + 3:000}";
        RoomTicket3.Text = $"M{_fallbackTicket:000}";
        RoomTicket4.Text = $"F{_fallbackTicket + 4:000}";
        RoomTicket5.Text = $"M{_fallbackTicket + 5:000}";
        SetQueueRows(new[]
        {
            new TicketDto($"M{_fallbackTicket + 1:000}", "Male", "Oromo", "Waiting"),
            new TicketDto($"F{_fallbackTicket + 2:000}", "Female", "Amharic", "Waiting"),
            new TicketDto($"M{_fallbackTicket + 3:000}", "Male", "English", "Waiting"),
            new TicketDto($"F{_fallbackTicket + 4:000}", "Female", "Oromo", "Waiting"),
            new TicketDto($"M{_fallbackTicket + 5:000}", "Male", "Amharic", "Waiting"),
            new TicketDto($"F{_fallbackTicket + 6:000}", "Female", "English", "Waiting")
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
