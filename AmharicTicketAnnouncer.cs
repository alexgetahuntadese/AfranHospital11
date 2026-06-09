using System.IO;
using System.Runtime.InteropServices;

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
                PlayAudioFile(audioPath);
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
        foreach (var extension in new[] { ".wav", ".mp3" })
        {
            var exactTicketAudio = Path.Combine(_voiceRoot, $"{safeTicket}{extension}");
            if (File.Exists(exactTicketAudio))
            {
                return exactTicketAudio;
            }
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

    private static void PlayAudioFile(string path)
    {
        var alias = $"ticket_{Guid.NewGuid():N}";
        var mediaType = Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase)
            ? "waveaudio"
            : "mpegvideo";
        var openCommand = $"open \"{path}\" type {mediaType} alias {alias}";
        try
        {
            SendMciCommand(openCommand);
            SendMciCommand($"play {alias} wait");
        }
        finally
        {
            _ = mciSendString($"close {alias}", null, 0, IntPtr.Zero);
        }
    }

    private static void SendMciCommand(string command)
    {
        var error = mciSendString(command, null, 0, IntPtr.Zero);
        if (error != 0)
        {
            throw new InvalidOperationException($"Audio playback failed: {error}");
        }
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, string? returnValue, int returnLength, IntPtr callback);
}
