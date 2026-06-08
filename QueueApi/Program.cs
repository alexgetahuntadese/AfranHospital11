using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5000");
builder.Services.AddDbContext<QueueDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("QueueDb") ?? "Data Source=SQLite.db"));
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QueueDb>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new { service = "Afran Queue API", status = "Running" }));

app.MapPost("/api/tickets", async (
    CreateTicketRequest request,
    QueueDb db,
    IHubContext<QueueHub> hub) =>
{
    var gender = NormalizeGender(request.Gender);
    var language = string.IsNullOrWhiteSpace(request.Language) ? "English" : request.Language.Trim();
    var prefix = gender.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "M" : "F";
    var nextNumber = await db.Tickets.CountAsync(t => t.Prefix == prefix) + 1;
    var ticketCode = $"{prefix}{nextNumber:000}";

    var ticket = new Ticket
    {
        TicketCode = ticketCode,
        Prefix = prefix,
        Gender = gender,
        Language = language,
        Status = TicketStatus.Waiting,
        CreatedAt = DateTime.UtcNow
    };

    db.Tickets.Add(ticket);
    db.QueueEvents.Add(QueueEvent.Created(ticketCode));
    await db.SaveChangesAsync();

    var display = await QueueDisplay.Load(db);
    await hub.Clients.All.SendAsync("TicketCreated", TicketDto.From(ticket));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(new TicketResponse(ticket.TicketCode));
});

app.MapPost("/api/queue/registration/call-next", async (QueueDb db, IHubContext<QueueHub> hub) =>
{
    var waiting = await db.Tickets
        .Where(t => t.Status == TicketStatus.Waiting)
        .OrderBy(t => t.CreatedAt)
        .FirstOrDefaultAsync();

    if (waiting is null)
    {
        return Results.NotFound(new { message = "No waiting tickets." });
    }

    var calledAt = DateTime.UtcNow;
    var previouslyCalled = await db.Tickets
        .Where(t => t.Status == TicketStatus.Called)
        .ToListAsync();
    foreach (var ticket in previouslyCalled)
    {
        ticket.Status = TicketStatus.Completed;
        ticket.CompletedAt = calledAt;
        db.QueueEvents.Add(QueueEvent.Completed(ticket.TicketCode));
    }

    waiting.Status = TicketStatus.Called;
    waiting.CalledAt = calledAt;
    db.QueueEvents.Add(QueueEvent.Called(waiting.TicketCode));
    await db.SaveChangesAsync();

    var display = await QueueDisplay.Load(db);
    await hub.Clients.All.SendAsync("TicketCalled", TicketDto.From(waiting));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(new TicketResponse(waiting.TicketCode));
});

app.MapPost("/api/queue/registration/complete", async (QueueDb db, IHubContext<QueueHub> hub) =>
{
    var called = await db.Tickets
        .Where(t => t.Status == TicketStatus.Called)
        .OrderByDescending(t => t.CalledAt)
        .FirstOrDefaultAsync();

    if (called is null)
    {
        return Results.NotFound(new { message = "No called ticket to complete." });
    }

    called.Status = TicketStatus.Completed;
    called.CompletedAt = DateTime.UtcNow;
    db.QueueEvents.Add(QueueEvent.Completed(called.TicketCode));
    await db.SaveChangesAsync();

    var display = await QueueDisplay.Load(db);
    await hub.Clients.All.SendAsync("TicketCompleted", TicketDto.From(called));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(new TicketResponse(called.TicketCode));
});

app.MapGet("/api/queue/registration/display", async (QueueDb db) =>
{
    return Results.Ok(await QueueDisplay.Load(db));
});

app.MapHub<QueueHub>("/queueHub");

app.Run();

static string NormalizeGender(string? gender)
{
    return gender?.Trim().Equals("Male", StringComparison.OrdinalIgnoreCase) == true ? "Male" : "Female";
}

public sealed class QueueHub : Hub
{
}

public sealed class QueueDb(DbContextOptions<QueueDb> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<QueueEvent> QueueEvents => Set<QueueEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>().HasIndex(ticket => ticket.TicketCode).IsUnique();
    }
}

public sealed class Ticket
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Language { get; set; } = "";
    public string Status { get; set; } = TicketStatus.Waiting;
    public DateTime CreatedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class QueueEvent
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public static QueueEvent Created(string ticket) => New(ticket, "Created");
    public static QueueEvent Called(string ticket) => New(ticket, "Called");
    public static QueueEvent Completed(string ticket) => New(ticket, "Completed");

    private static QueueEvent New(string ticket, string type) => new()
    {
        TicketCode = ticket,
        EventType = type,
        CreatedAt = DateTime.UtcNow
    };
}

public static class TicketStatus
{
    public const string Waiting = "Waiting";
    public const string Called = "Called";
    public const string Completed = "Completed";
}

public sealed record CreateTicketRequest(string? Gender, string? Language);
public sealed record TicketResponse(string Ticket);
public sealed record TicketDto(string Ticket, string Gender, string Language, string Status)
{
    public static TicketDto From(Ticket ticket)
    {
        return new TicketDto(ticket.TicketCode, ticket.Gender, ticket.Language, ticket.Status);
    }
}

public sealed record QueueDisplay(TicketDto? NowServing, IReadOnlyList<TicketDto> Waiting, int WaitingCount)
{
    public static async Task<QueueDisplay> Load(QueueDb db)
    {
        var called = await db.Tickets
            .Where(t => t.Status == TicketStatus.Called)
            .OrderByDescending(t => t.CalledAt)
            .FirstOrDefaultAsync();

        var waiting = await db.Tickets
            .Where(t => t.Status == TicketStatus.Waiting)
            .OrderBy(t => t.CreatedAt)
            .Take(6)
            .ToListAsync();

        var waitingCount = await db.Tickets.CountAsync(t => t.Status == TicketStatus.Waiting);

        return new QueueDisplay(
            called is null ? null : TicketDto.From(called),
            waiting.Select(TicketDto.From).ToList(),
            waitingCount);
    }
}
