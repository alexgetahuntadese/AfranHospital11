using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace AfranHospitalKiosk;

public sealed class QueueApiClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private HubConnection? _hubConnection;

    public QueueApiClient()
    {
        BaseUrl = Environment.GetEnvironmentVariable("AFRAN_QUEUE_API")?.TrimEnd('/')
            ?? "http://localhost:5000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        var apiKey = Environment.GetEnvironmentVariable("AFRAN_QUEUE_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
        }
    }

    public string BaseUrl { get; }

    public async Task<string?> CreateTicketAsync(string gender, string language)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(gender, language));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketResponse>())?.Ticket;
    }

    public async Task<TicketDto?> CallNextAsync()
    {
        return await PostQueueActionAsync("/api/queue/registration/call-next");
    }

    public async Task<TicketDto?> CompleteAsync()
    {
        return await PostQueueActionAsync("/api/queue/registration/complete");
    }

    public async Task<TicketDto?> RecallCurrentAsync()
    {
        return await PostQueueActionAsync("/api/queue/registration/recall");
    }

    public async Task<QueueDisplay?> GetDisplayAsync()
    {
        return await _httpClient.GetFromJsonAsync<QueueDisplay>("/api/queue/registration/display");
    }

    public async Task ConnectHubAsync(Action<QueueDisplay> onQueueUpdated, Action<TicketDto>? onTicketCalled = null)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{BaseUrl}/queueHub")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On("QueueUpdated", onQueueUpdated);
        if (onTicketCalled is not null)
        {
            _hubConnection.On("TicketCalled", onTicketCalled);
        }

        await _hubConnection.StartAsync();
    }

    private async Task<TicketDto?> PostQueueActionAsync(string endpoint)
    {
        using var response = await _httpClient.PostAsync(endpoint, null);

        // A missing ticket is a valid queue state, not an API outage. Other
        // failures must remain exceptions so the doctor screen does not make
        // up a local ticket for an unsynced operation.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TicketResponse>();
        return result is null
            ? null
            : new TicketDto(result.Ticket, result.Gender, result.Language, result.Status, result.CreatedAt);
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
        await ValueTask.CompletedTask;
    }

    private sealed record CreateTicketRequest(string Gender, string Language);
    private sealed record TicketResponse(string Ticket, string Gender, string Language, string Status, DateTime CreatedAt);
}

public sealed record TicketDto(string Ticket, string Gender, string Language, string Status, DateTime CreatedAt)
{
    public string TimeText => CreatedAt.ToLocalTime().ToString("HH:mm");
}
public sealed record QueueDisplay(TicketDto? NowServing, IReadOnlyList<TicketDto> Waiting, int WaitingCount);
