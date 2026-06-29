using System.IO;
using System.Windows;
using System.Windows.Media;

namespace AfranHospitalKiosk;

public sealed class AmharicTicketAnnouncer
{
    private const int RepeatCount = 2;
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(350);
    private readonly SemaphoreSlim _speechLock = new(1, 1);
    private readonly string _voiceRoot;
    private readonly Dictionary<string, string> _ticketVoices;

    public AmharicTicketAnnouncer()
    {
        _voiceRoot = FindVoiceRoot();
        _ticketVoices = LoadTicketVoices(_voiceRoot);
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
            for (var repeat = 0; repeat < RepeatCount; repeat++)
            {
                await PlayAudioFileAsync(audioPath);
                if (repeat + 1 < RepeatCount)
                {
                    await Task.Delay(RepeatDelay);
                    var repeatAudio = FindRepeatAudio(ticket);
                    if (repeatAudio is not null)
                    {
                        await PlayAudioFileAsync(repeatAudio);
                    }

                    await Task.Delay(RepeatDelay);
                }
            }
        }
        catch (Exception ex)
        {
            // Voice playback is optional; the TV queue display should keep running.
            System.Diagnostics.Debug.WriteLine($"Audio Playback Error: {ex.Message}");
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

    private string? FindRepeatAudio(string? ticket)
    {
        var voice = "female";
        if (!string.IsNullOrWhiteSpace(ticket)
            && _ticketVoices.TryGetValue(ticket.Trim().ToUpperInvariant(), out var manifestVoice))
        {
            voice = manifestVoice;
        }

        var preferred = Path.Combine(_voiceRoot, $"repeat-{voice}.mp3");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        var fallback = Path.Combine(_voiceRoot, "repeat-female.mp3");
        return File.Exists(fallback) ? fallback : null;
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

    private static Dictionary<string, string> LoadTicketVoices(string voiceRoot)
    {
        var manifestPath = Path.Combine(voiceRoot, "random-voices-both-001-999.txt");
        var voices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(manifestPath))
        {
            return voices;
        }

        foreach (var line in File.ReadLines(manifestPath))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                voices[parts[0]] = parts[1].Equals("male", StringComparison.OrdinalIgnoreCase)
                    ? "male"
                    : "female";
            }
        }

        return voices;
    }

    private static Task PlayAudioFileAsync(string path)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Invoke(() =>
        {
            var player = new MediaPlayer();
            var timeout = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };

            void ClosePlayer()
            {
                timeout.Stop();
                player.Close();
            }

            timeout.Tick += (_, _) =>
            {
                ClosePlayer();
                completion.TrySetResult();
            };

            player.MediaEnded += (_, _) =>
            {
                ClosePlayer();
                completion.TrySetResult();
            };

            player.MediaFailed += (_, _) =>
            {
                ClosePlayer();
                completion.TrySetResult();
            };

            player.Open(new Uri(path, UriKind.Absolute));
            player.Volume = 1.0;
            timeout.Start();
            player.Play();
        });

        return completion.Task;
    }
}
