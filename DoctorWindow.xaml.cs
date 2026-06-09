using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AfranHospitalKiosk;

public partial class DoctorWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly QueueApiClient _apiClient = new();
    private readonly AmharicTicketAnnouncer _announcer = new();
    private int _fallbackTicket = 105;
    private bool _isBusy;

    public DoctorWindow()
    {
        InitializeComponent();
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        _ = ConnectApiAsync();
    }

    private async Task ConnectApiAsync()
    {
        try
        {
            await RefreshDisplayAsync();
            await _apiClient.ConnectHubAsync(display =>
            {
                Dispatcher.Invoke(() => ApplyDisplay(display));
            });
            RoomLabel.Text = $"Connected to {_apiClient.BaseUrl}";
        }
        catch
        {
            RoomLabel.Text = "API offline. Using local demo controls.";
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
        NowCallingLabel.Text = display.NowServing?.Ticket ?? "-";
        if (display.NowServing is not null)
        {
            RoomLabel.Text = "Doctor Room 3";
        }

        SetQueueRow(1, display.Waiting.ElementAtOrDefault(0));
        SetQueueRow(2, display.Waiting.ElementAtOrDefault(1));
        SetQueueRow(3, display.Waiting.ElementAtOrDefault(2));
    }

    private void SetQueueRow(int row, TicketDto? ticket)
    {
        var ticketText = ticket?.Ticket ?? "-";
        var languageText = ticket?.Language ?? "-";
        var genderText = ticket?.Gender ?? "-";

        switch (row)
        {
            case 1:
                QueueTicket1.Text = ticketText;
                QueueLanguage1.Text = languageText;
                QueueGender1.Text = genderText;
                break;
            case 2:
                QueueTicket2.Text = ticketText;
                QueueLanguage2.Text = languageText;
                QueueGender2.Text = genderText;
                break;
            case 3:
                QueueTicket3.Text = ticketText;
                QueueLanguage3.Text = languageText;
                QueueGender3.Text = genderText;
                break;
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockLabel.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        DateLabel.Text = now.ToString("dddd, dd MMM yyyy", CultureInfo.CurrentCulture);
    }

    private async void CallNext_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            var ticket = await _apiClient.CallNextAsync();
            NowCallingLabel.Text = ticket ?? "-";
            RoomLabel.Text = ticket is null ? "No waiting tickets." : "Doctor Room 3";
            if (ticket is not null)
            {
                _ = _announcer.AnnounceAsync(ticket);
            }

            await RefreshDisplayAsync();
        }
        catch
        {
            _fallbackTicket++;
            var ticket = $"M{_fallbackTicket:000}";
            NowCallingLabel.Text = ticket;
            RoomLabel.Text = "API offline. Local call only.";
            _ = _announcer.AnnounceAsync(ticket);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void Recall_Click(object sender, RoutedEventArgs e)
    {
        var ticket = NowCallingLabel.Text;
        if (string.IsNullOrWhiteSpace(ticket) || ticket == "-")
        {
            RoomLabel.Text = "No called ticket to recall.";
            return;
        }

        _ = RecallCurrentAsync(ticket);
    }

    private async Task RecallCurrentAsync(string ticket)
    {
        try
        {
            var recalled = await _apiClient.RecallCurrentAsync();
            RoomLabel.Text = recalled is null
                ? "No called ticket to recall."
                : $"Recalling {recalled}.";
            if (recalled is not null)
            {
                _ = _announcer.AnnounceAsync(recalled);
            }
        }
        catch
        {
            RoomLabel.Text = $"Recalling {ticket}. API offline.";
        }
    }

    private async void Complete_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            var completed = await _apiClient.CompleteAsync();
            var next = await _apiClient.CallNextAsync();
            NowCallingLabel.Text = next ?? "-";
            RoomLabel.Text = next is not null
                ? $"Completed {completed}. Calling {next}."
                : completed is not null
                    ? $"Completed {completed}. No waiting tickets."
                    : "No called ticket to complete.";
            if (next is not null)
            {
                _ = _announcer.AnnounceAsync(next);
            }

            await RefreshDisplayAsync();
        }
        catch
        {
            RoomLabel.Text = "API offline. Complete was not synced.";
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _isBusy = !enabled;
        CallNextButton.IsEnabled = enabled;
        RecallButton.IsEnabled = enabled;
        CompleteButton.IsEnabled = enabled;
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
