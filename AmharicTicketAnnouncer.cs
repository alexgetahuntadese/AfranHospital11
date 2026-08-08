using System.IO;
using System.Speech.Synthesis;
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

    public async Task AnnounceAsync(string? ticket, string? language = null, string? roomNumber = null)
    {
        var audioRoot = FindVoiceRoot(language);
        var ticketAudio = FindTicketAudio(ticket, audioRoot);
        var cleanedRoomNumber = string.IsNullOrWhiteSpace(roomNumber) ? null : roomNumber.Trim();
        var shouldUseTicketAudio = ticketAudio is not null && !ShouldSkipTicketAudioForRoomMismatch(ticket, cleanedRoomNumber, language);
        var isEmbeddedLanguage = IsEmbeddedRoomInTicketAudio(language);

        await _speechLock.WaitAsync();
        try
        {
            if (shouldUseTicketAudio && !isEmbeddedLanguage)
            {
                for (var repeat = 0; repeat < RepeatCount; repeat++)
                {
                    await PlayAudioFileAsync(ticketAudio!);
                    if (repeat + 1 < RepeatCount)
                    {
                        await Task.Delay(RepeatDelay);
                        var repeatAudio = FindRepeatAudio(ticket, audioRoot);
                        await PlayAudioFileAsync(repeatAudio ?? ticketAudio);
                        await Task.Delay(RepeatDelay);
                    }
                }
                return;
            }

            if (shouldUseTicketAudio && !string.IsNullOrWhiteSpace(cleanedRoomNumber) && isEmbeddedLanguage)
            {
                await SpeakTextAsync(BuildFallbackAnnouncement(ticket, cleanedRoomNumber, language));
                return;
            }

            if (shouldUseTicketAudio)
            {
                for (var repeat = 0; repeat < RepeatCount; repeat++)
                {
                    await PlayAudioFileAsync(ticketAudio!);
                    if (repeat + 1 < RepeatCount)
                    {
                        await Task.Delay(RepeatDelay);
                        var repeatAudio = FindRepeatAudio(ticket, audioRoot);
                        await PlayAudioFileAsync(repeatAudio ?? ticketAudio);
                        await Task.Delay(RepeatDelay);
                    }
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(ticket))
            {
                await SpeakTextAsync(BuildFallbackAnnouncement(ticket, cleanedRoomNumber, language));
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

    private static string? FindTicketAudio(string? ticket, string voiceRoot)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket == "-")
        {
            return null;
        }

        var safeTicket = SafeFilePart(ticket.Trim().ToUpperInvariant());
        foreach (var extension in new[] { ".wav", ".mp3" })
        {
            var exactTicketAudio = Path.Combine(voiceRoot, $"{safeTicket}{extension}");
            if (File.Exists(exactTicketAudio))
            {
                return exactTicketAudio;
            }
        }

        return null;
    }

    private static string? FindRepeatAudio(string? ticket, string voiceRoot)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        var safeTicket = SafeFilePart(ticket.Trim().ToUpperInvariant());
        foreach (var extension in new[] { ".wav", ".mp3" })
        {
            var repeatFile = Path.Combine(voiceRoot, $"{safeTicket}-repeat{extension}");
            if (File.Exists(repeatFile))
            {
                return repeatFile;
            }
        }

        return null;
    }

    private static string? FindRoomAudio(string roomNumber, string voiceRoot)
    {
        var safeRoom = SafeFilePart(roomNumber.ToUpperInvariant());
        foreach (var extension in new[] { ".wav", ".mp3" })
        {
            var roomFile = Path.Combine(voiceRoot, $"Room{safeRoom}{extension}");
            if (File.Exists(roomFile))
            {
                return roomFile;
            }

            var roomFileAlt = Path.Combine(voiceRoot, $"room-{safeRoom}{extension}");
            if (File.Exists(roomFileAlt))
            {
                return roomFileAlt;
            }
        }

        return null;
    }

    private static async Task PlayRoomNumberAsync(string roomNumber, string voiceRoot, string? language = null)
    {
        var roomAudio = FindRoomAudio(roomNumber, voiceRoot);
        if (roomAudio is not null)
        {
            await PlayAudioFileAsync(roomAudio);
            return;
        }

        var roomPhrase = IsAmharic(language)
            ? $"ክፍል {roomNumber}"
            : IsOromo(language)
                ? $"kutaa {roomNumber}"
                : $"Room {roomNumber}";

        await SpeakTextAsync(roomPhrase);
    }

    private static bool ShouldSkipTicketAudioForRoomMismatch(string? ticket, string? roomNumber, string? language)
    {
        if (string.IsNullOrWhiteSpace(ticket) || string.IsNullOrWhiteSpace(roomNumber))
        {
            return false;
        }

        if (!IsEmbeddedRoomInTicketAudio(language))
        {
            return false;
        }

        var trimmedTicket = ticket.Trim().ToUpperInvariant();
        if (trimmedTicket.Length == 0)
        {
            return false;
        }

        var prefix = trimmedTicket[0];
        if (prefix == 'M' && roomNumber != "101")
        {
            return true;
        }

        if (prefix == 'F' && roomNumber != "102")
        {
            return true;
        }

        return false;
    }

    private static bool IsEmbeddedRoomInTicketAudio(string? language) => IsAmharic(language) || IsOromo(language);

    private static bool IsAmharic(string? language) =>
        language?.Trim().Equals("Amharic", StringComparison.OrdinalIgnoreCase) == true;

    private static string BuildFallbackAnnouncement(string ticket, string? roomNumber, string? language)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            return IsAmharic(language)
                ? $"ቁጥር {ticket}"
                : IsOromo(language)
                    ? $"Lakkoofsa {ticket}"
                    : $"Ticket {ticket}";
        }

        if (IsAmharic(language))
        {
            return $"ቁጥር {ticket} ወደ ክፍል {roomNumber} ይሂዱ።";
        }

        if (IsOromo(language))
        {
            return $"Lakkoofsa {ticket}, gara kutaa {roomNumber} deemaa.";
        }

        return $"Ticket {ticket}. Go to room {roomNumber}.";
    }

    private static bool IsOromo(string? language) =>
        language?.Trim().Equals("Oromo", StringComparison.OrdinalIgnoreCase) == true
        || language?.Trim().Equals("Afaan Oromo", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task SpeakTextAsync(string text)
    {
        await Task.Run(() =>
        {
            using var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.Speak(text);
        });
    }

    private static string FindVoiceRoot(string? language = null)
    {
        var folder = IsOromo(language) ? "Oromo" : "Amharic";
        var candidates = FindCandidateRoots(folder).ToList();

        var readyPath = candidates.FirstOrDefault(Directory.Exists);
        if (readyPath is not null)
        {
            return readyPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Assets", "Voices", folder);
    }

    private static IEnumerable<string> FindCandidateRoots(string folder)
    {
        foreach (var basePath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(basePath);
            while (directory is not null)
            {
                yield return Path.Combine(directory.FullName, "Assets", "Voices", folder);
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
