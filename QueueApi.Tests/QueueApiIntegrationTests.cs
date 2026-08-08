using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace QueueApi.Tests;

public sealed class QueueApiIntegrationTests : IAsyncLifetime
{
    private const string ApiKey = "integration-test-key";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"afran-queue-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Security:ApiKey", ApiKey);
            builder.UseSetting("ConnectionStrings:QueueDb", $"Data Source={_databasePath}");
            builder.ConfigureTestServices(services => { });
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Concurrent_ticket_creation_returns_unique_sequential_codes()
    {
        var requests = Enumerable.Range(0, 12).Select(_ =>
            _client.PostAsJsonAsync("/api/tickets", new { Gender = "Male", Language = "English" }));

        var responses = await Task.WhenAll(requests);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var tickets = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<TicketResponse>()));
        var numbers = tickets.Select(ticket => int.Parse(ticket!.Ticket[1..])).OrderBy(number => number).ToArray();
        Assert.Equal(numbers.Length, numbers.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, numbers.Length), numbers);
    }

    [Fact]
    public async Task Queue_transitions_create_call_and_complete_ticket()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/tickets", new { Gender = "Female", Language = "Amharic" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TicketResponse>();

        var callResponse = await _client.PostAsync("/api/queue/registration/call-next?roomNumber=102", null);
        callResponse.EnsureSuccessStatusCode();
        var called = await callResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.Equal(created!.Ticket, called!.Ticket);
        Assert.Equal("Called", called.Status);
        Assert.Equal("102", called.RoomNumber);

        var completeResponse = await _client.PostAsync("/api/queue/registration/complete", null);
        completeResponse.EnsureSuccessStatusCode();
        var completed = await completeResponse.Content.ReadFromJsonAsync<TicketResponse>();
        Assert.Equal(created.Ticket, completed!.Ticket);
        Assert.Equal("Completed", completed.Status);
    }

    private sealed record TicketResponse(string Ticket, string Gender, string Language, string Status, DateTime CreatedAt, string? RoomNumber);
}
