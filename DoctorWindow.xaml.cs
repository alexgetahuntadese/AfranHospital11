using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AfranHospitalKiosk;

public partial class DoctorWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _queueRefreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly QueueApiClient _apiClient = new();
    private readonly AmharicTicketAnnouncer _announcer = new();
    private readonly object _announcementLock = new();
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _lastAnnouncedTicket;
    private DateTime _lastAnnouncementUtc;

    public DoctorWindow()
    {
        InitializeComponent();
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        _queueRefreshTimer.Tick += async (_, _) => await RefreshDisplaySafelyAsync();
        _queueRefreshTimer.Start();
        
        _ = ConnectApiAsync();
    }

    private async Task ConnectApiAsync()
    {
        try
        {
            // Do not keep the action buttons disabled while the follow-up
            // display refresh is waiting on the network. The call itself has
            // already completed successfully at this point.
            _ = RefreshDisplaySafelyAsync();
            await _apiClient.ConnectHubAsync(display =>
            {
                Dispatcher.Invoke(() => ApplyDisplay(display));
            }, ticket =>
            {
                Dispatcher.Invoke(() =>
                {
                    NowCallingLabel.Text = ticket.Ticket;
                    RoomLabel.Text = "Doctor Room 3";
                });
                _ = AnnounceTicketOnceAsync(ticket.Ticket);
            });
            RoomLabel.Text = displayRoomText();
        }
        catch (Exception ex)
        {
            RoomLabel.Text = "API offline. Please check connection.";
            System.Diagnostics.Debug.WriteLine($"API Connection Error: {ex.Message}");
        }
    }

    private async Task RefreshDisplayAsync()
    {
        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var display = await _apiClient.GetDisplayAsync();
            if (display is not null)
            {
                ApplyDisplay(display);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshDisplaySafelyAsync()
    {
        try
        {
            // Refresh the queue in the background so Complete and Call Next
            // buttons are re-enabled as soon as the action has completed.
            _ = RefreshDisplaySafelyAsync();
        }
        catch (Exception ex)
        {
            RoomLabel.Text = "API offline. Please check connection.";
            System.Diagnostics.Debug.WriteLine($"Queue refresh error: {ex.Message}");
        }
    }

    private string displayRoomText() => "Doctor Room 3";

    private void ApplyDisplay(QueueDisplay display)
    {
        NowCallingLabel.Text = display.NowServing?.Ticket ?? "-";
        if (display.NowServing is not null)
        {
            RoomLabel.Text = "Doctor Room 3";
        }

        WaitingTicketsList.ItemsSource = display.Waiting;
        WaitingCountLabel.Text = $"{display.WaitingCount} waiting";
        WaitingEmptyLabel.Visibility = display.Waiting.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockLabel.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        DateLabel.Text = now.ToString("dddd, dd MMM yyyy", CultureInfo.CurrentCulture);
    }

    private async void CallNext_Click(object sender, RoutedEventArgs e)
    {
        if (!await TryEnterActionAsync()) return;

        try
        {
            var ticket = await _apiClient.CallNextAsync();
            NowCallingLabel.Text = ticket ?? "-";
            RoomLabel.Text = ticket is null ? "No waiting tickets." : "Doctor Room 3";
            if (ticket is not null)
            {
                _ = AnnounceTicketOnceAsync(ticket);
            }

            await RefreshDisplayAsync();
        }
        catch (Exception ex)
        {
            RoomLabel.Text = "Call failed. API is unavailable.";
            System.Diagnostics.Debug.WriteLine($"Call Next Error: {ex.Message}");
        }
        finally
        {
            LeaveAction();
        }
    }

    private async void Recall_Click(object sender, RoutedEventArgs e)
    {
        var ticket = NowCallingLabel.Text;
        if (string.IsNullOrWhiteSpace(ticket) || ticket == "-")
        {
            RoomLabel.Text = "No called ticket to recall.";
            return;
        }

        if (!await TryEnterActionAsync()) return;
        try
        {
            await RecallCurrentAsync(ticket);
        }
        finally
        {
            LeaveAction();
        }
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
                _ = AnnounceTicketOnceAsync(recalled);
            }
        }
        catch (Exception ex)
        {
            RoomLabel.Text = $"Recalling {ticket}. API offline.";
            System.Diagnostics.Debug.WriteLine($"Recall Error: {ex.Message}");
        }
    }

    private async void Complete_Click(object sender, RoutedEventArgs e)
    {
        if (!await TryEnterActionAsync()) return;

        try
        {
            var completed = await _apiClient.CompleteAsync();
            if (completed is null)
            {
                RoomLabel.Text = "No called ticket to complete.";
                await RefreshDisplaySafelyAsync();
                return;
            }

            var next = await _apiClient.CallNextAsync();
            NowCallingLabel.Text = next ?? "-";
            RoomLabel.Text = next is not null
                ? $"Completed {completed}. Calling {next}."
                : $"Completed {completed}. No waiting tickets.";
            if (next is not null)
            {
                _ = AnnounceTicketOnceAsync(next);
            }

            await RefreshDisplayAsync();
        }
        catch (Exception ex)
        {
            RoomLabel.Text = "Complete failed. API is unavailable.";
            System.Diagnostics.Debug.WriteLine($"Complete Error: {ex.Message}");
        }
        finally
        {
            LeaveAction();
        }
    }

    private async Task<bool> TryEnterActionAsync()
    {
        if (!await _actionLock.WaitAsync(0)) return false;
        CallNextButton.IsEnabled = false;
        RecallButton.IsEnabled = false;
        CompleteButton.IsEnabled = false;
        return true;
    }

    private void LeaveAction()
    {
        CallNextButton.IsEnabled = true;
        RecallButton.IsEnabled = true;
        CompleteButton.IsEnabled = true;
        _actionLock.Release();
    }

    private Task AnnounceTicketOnceAsync(string ticket)
    {
        lock (_announcementLock)
        {
            if (string.Equals(_lastAnnouncedTicket, ticket, StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - _lastAnnouncementUtc < TimeSpan.FromSeconds(10))
            {
                return Task.CompletedTask;
            }

            _lastAnnouncedTicket = ticket;
            _lastAnnouncementUtc = DateTime.UtcNow;
        }

        // AmharicTicketAnnouncer intentionally plays one announcement and one
        // repeat (two plays total). This guard prevents the local API response
        // and the SignalR TicketCalled event from announcing the same ticket twice.
        return _announcer.AnnounceAsync(ticket);
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
        _clockTimer.Stop();
        _queueRefreshTimer.Stop();
        await _apiClient.DisposeAsync();
        _actionLock.Dispose();
        _refreshLock.Dispose();
        base.OnClosed(e);
    }
}
