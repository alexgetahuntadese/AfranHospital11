using System.IO;
using System.Media;

namespace AfranHospitalKiosk;

public sealed class AmharicTicketAnnouncer
{
    private readonly SemaphoreSlim _speechLock = new(1, 1);
    private readonly string _voiceRoot;

    public AmharicTicketAnnouncer()
    {
        _voiceRoot = FindVoiceRoot();
    }

    public bool IsReady => Directory.Exists(_voiceRoot);

    public async Task AnnounceAsync(string? ticket)
    {
        var audioPath = FindTicketAudio(ticket);
        if (audioPath is null)
        {
            return;
        }

        await _speechLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var player = new SoundPlayer(audioPath);
                player.PlaySync();
            });
        }
        catch
        {
            // Voice playback is optional; the TV queue display should keep running.
        }
        finally
        {
            _speechLock.Release();
        }
    }

    private string? FindTicketAudio(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket == "-")
        {
            return null;
        }

        var safeTicket = SafeFilePart(ticket.Trim().ToUpperInvariant());
        var exactTicketAudio = Path.Combine(_voiceRoot, $"{safeTicket}.wav");
        if (File.Exists(exactTicketAudio))
        {
            return exactTicketAudio;
        }

        return null;
    }

    private static string FindVoiceRoot()
    {
        var candidates = FindCandidateRoots().ToList();

        var readyPath = candidates.FirstOrDefault(Directory.Exists);
        if (readyPath is not null)
        {
            return readyPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Assets", "Voices", "Amharic");
    }

    private static IEnumerable<string> FindCandidateRoots()
    {
        foreach (var basePath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(basePath);
            while (directory is not null)
            {
                yield return Path.Combine(directory.FullName, "Assets", "Voices", "Amharic");
                directory = directory.Parent;
            }
        }
    }

    private static string SafeFilePart(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}
