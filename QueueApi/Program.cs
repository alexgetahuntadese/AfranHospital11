using System.Data.Common;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5000");
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff'Z' ";
    options.SingleLine = true;
});
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<QueueDb>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("QueueDb") ?? "Data Source=SQLite.db",
        sqlite => sqlite.CommandTimeout(30)));
builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("queue-mutations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("lan-clients", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins);
        }
        else
        {
            policy.SetIsOriginAllowed(IsAllowedLanOrigin);
        }
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
var configuredApiKey = builder.Configuration["Security:ApiKey"]?.Trim();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !HttpMethods.IsGet(context.Request.Method) &&
        !HttpMethods.IsHead(context.Request.Method))
    {
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "Queue mutation authentication is not configured." });
            return;
        }

        if (!string.Equals(context.Request.Headers["X-Api-Key"], configuredApiKey, StringComparison.Ordinal))
        {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "A valid X-Api-Key is required." });
        return;
        }
    }

    await next();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QueueDb>();
    db.Database.EnsureCreated();
    await EnsureRoomNumberColumnAsync(db);
    await EnsureSequenceTableAsync(db);
}

app.UseCors("lan-clients");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new { service = "HIWOT FANA INTERNAL MEDICINE SPECIALTY CLINIC QUEUE API", status = "Running" }));
app.MapHealthChecks("/health/live");
app.MapGet("/health/ready", async (QueueDb db, CancellationToken cancellationToken) =>
{
    var healthy = await db.Database.CanConnectAsync(cancellationToken);
    return healthy
        ? Results.Ok(new { status = "Ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapPost("/api/tickets", async (
    CreateTicketRequest request,
    QueueDb db,
    IHubContext<QueueHub> hub,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request is null || request.Language?.Length > 50)
    {
        return Results.BadRequest(new { message = "Language must be 50 characters or fewer." });
    }

    var gender = NormalizeGender(request.Gender);
    var language = string.IsNullOrWhiteSpace(request.Language) ? "English" : request.Language.Trim();
    var prefix = gender.Equals("Male", StringComparison.OrdinalIgnoreCase) ? "M" : "F";
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var nextNumber = await GetNextTicketNumberAsync(db, prefix, cancellationToken);
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
    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    logger.LogInformation("Ticket {TicketCode} created with prefix {Prefix}.", ticketCode, prefix);

    var display = await QueueDisplay.Load(db);
    await hub.Clients.All.SendAsync("TicketCreated", TicketDto.From(ticket));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(TicketResponse.From(ticket));
}).RequireRateLimiting("queue-mutations");

app.MapPost("/api/queue/registration/call-next", async (string? roomNumber, QueueDb db, IHubContext<QueueHub> hub, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var waiting = await db.Tickets
        .Where(t => t.Status == TicketStatus.Waiting)
        .OrderBy(t => t.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    if (waiting is null)
    {
        return Results.NotFound(new { message = "No waiting tickets." });
    }

    var room = string.IsNullOrWhiteSpace(roomNumber)
        ? waiting.RoomNumber ?? "3"
        : roomNumber.Trim();

    var calledAt = DateTime.UtcNow;
    var previouslyCalled = await db.Tickets
        .Where(t => t.Status == TicketStatus.Called)
        .ToListAsync(cancellationToken);
    foreach (var ticket in previouslyCalled)
    {
        ticket.Status = TicketStatus.Completed;
        ticket.CompletedAt = calledAt;
        db.QueueEvents.Add(QueueEvent.Completed(ticket.TicketCode));
    }

    waiting.Status = TicketStatus.Called;
    waiting.CalledAt = calledAt;
    waiting.RoomNumber = room;
    db.QueueEvents.Add(QueueEvent.Called(waiting.TicketCode));
    await db.SaveChangesAsync(cancellationToken);
    logger.LogInformation("Ticket {TicketCode} called for registration in room {RoomNumber}.", waiting.TicketCode, waiting.RoomNumber);

    var display = await QueueDisplay.Load(db);
    await hub.Clients.All.SendAsync("TicketCalled", TicketDto.From(waiting));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(TicketResponse.From(waiting));
}).RequireRateLimiting("queue-mutations");

app.MapPost("/api/queue/registration/complete", async (QueueDb db, IHubContext<QueueHub> hub, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var called = await db.Tickets
        .Where(t => t.Status == TicketStatus.Called)
        .OrderByDescending(t => t.CalledAt)
        .FirstOrDefaultAsync(cancellationToken);

    if (called is null)
    {
        return Results.NotFound(new { message = "No called ticket to complete." });
    }

    called.Status = TicketStatus.Completed;
    called.CompletedAt = DateTime.UtcNow;
    db.QueueEvents.Add(QueueEvent.Completed(called.TicketCode));
    await db.SaveChangesAsync(cancellationToken);
    logger.LogInformation("Ticket {TicketCode} completed.", called.TicketCode);

    var display = await QueueDisplay.Load(db);
    await hub.Clients.All.SendAsync("TicketCompleted", TicketDto.From(called));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(TicketResponse.From(called));
}).RequireRateLimiting("queue-mutations");

app.MapPost("/api/queue/registration/recall", async (QueueDb db, IHubContext<QueueHub> hub, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var called = await db.Tickets
        .Where(t => t.Status == TicketStatus.Called)
        .OrderByDescending(t => t.CalledAt)
        .FirstOrDefaultAsync(cancellationToken);

    if (called is null)
    {
        return Results.NotFound(new { message = "No called ticket to recall." });
    }

    var display = await QueueDisplay.Load(db);
    logger.LogInformation("Ticket {TicketCode} recalled.", called.TicketCode);
    await hub.Clients.All.SendAsync("TicketCalled", TicketDto.From(called));
    await hub.Clients.All.SendAsync("QueueUpdated", display);

    return Results.Ok(TicketResponse.From(called));
}).RequireRateLimiting("queue-mutations");

app.MapGet("/api/queue/registration/display", async (QueueDb db) =>
{
    return Results.Ok(await QueueDisplay.Load(db));
});

app.MapHub<QueueHub>("/queueHub");

app.Run();

static bool IsAllowedLanOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
    {
        return false;
    }

    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
    if (!IPAddress.TryParse(uri.Host, out var address)) return false;
    if (IPAddress.IsLoopback(address)) return true;

    var bytes = address.GetAddressBytes();
    return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
           (bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168));
}

static string NormalizeGender(string? gender)
{
    return gender?.Trim().Equals("Male", StringComparison.OrdinalIgnoreCase) == true ? "Male" : "Female";
}

static async Task EnsureRoomNumberColumnAsync(QueueDb db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Tickets');";
        using var reader = await command.ExecuteReaderAsync();
        var hasRoomNumber = false;
        while (await reader.ReadAsync())
        {
            if (reader.GetString(reader.GetOrdinal("name")).Equals("RoomNumber", StringComparison.OrdinalIgnoreCase))
            {
                hasRoomNumber = true;
                break;
            }
        }

        if (!hasRoomNumber)
        {
            using var addColumn = connection.CreateCommand();
            addColumn.CommandText = "ALTER TABLE Tickets ADD COLUMN RoomNumber TEXT;";
            await addColumn.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task EnsureSequenceTableAsync(QueueDb db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS TicketSequences (
                Prefix TEXT NOT NULL PRIMARY KEY,
                NextNumber INTEGER NOT NULL
            );
            INSERT OR IGNORE INTO TicketSequences (Prefix, NextNumber)
            SELECT Prefix, COALESCE(MAX(CAST(SUBSTR(TicketCode, 2) AS INTEGER)), 0)
            FROM Tickets
            GROUP BY Prefix;
            """;
        await command.ExecuteNonQueryAsync();
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<int> GetNextTicketNumberAsync(QueueDb db, string prefix, CancellationToken cancellationToken)
{
    var connection = db.Database.GetDbConnection();
    await using var command = connection.CreateCommand();
    command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
    command.CommandText = """
        INSERT INTO TicketSequences (Prefix, NextNumber)
        VALUES ($prefix, 1)
        ON CONFLICT(Prefix) DO UPDATE SET NextNumber = NextNumber + 1
        RETURNING NextNumber;
        """;
    var parameter = command.CreateParameter();
    parameter.ParameterName = "$prefix";
    parameter.Value = prefix;
    command.Parameters.Add(parameter);
    return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
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
    public string? RoomNumber { get; set; }
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
public sealed record TicketResponse(string Ticket, string Gender, string Language, string Status, DateTime CreatedAt, string? RoomNumber = null)
{
    public static TicketResponse From(Ticket ticket) =>
        new(ticket.TicketCode, ticket.Gender, ticket.Language, ticket.Status, ticket.CreatedAt, ticket.RoomNumber);
}
public sealed record TicketDto(string Ticket, string Gender, string Language, string Status, DateTime CreatedAt, string? RoomNumber = null)
{
    public static TicketDto From(Ticket ticket)
    {
        return new TicketDto(ticket.TicketCode, ticket.Gender, ticket.Language, ticket.Status, ticket.CreatedAt, ticket.RoomNumber);
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
            .ToListAsync();

        var waitingCount = await db.Tickets.CountAsync(t => t.Status == TicketStatus.Waiting);

        return new QueueDisplay(
            called is null ? null : TicketDto.From(called),
            waiting.Select(TicketDto.From).ToList(),
            waitingCount);
    }
}

public partial class Program;
