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

        await _speechLock.WaitAsync();
        try
        {
            if (ticketAudio is not null)
            {
                for (var repeat = 0; repeat < RepeatCount; repeat++)
                {
                    await PlayAudioFileAsync(ticketAudio);
                    if (repeat + 1 < RepeatCount)
                    {
                        await Task.Delay(RepeatDelay);
                        var repeatAudio = FindRepeatAudio(ticket, audioRoot);
                        if (repeatAudio is not null)
                        {
                            await PlayAudioFileAsync(repeatAudio);
                        }

                        await Task.Delay(RepeatDelay);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(ticket))
            {
                await SpeakTextAsync($"Ticket {ticket}");
            }

            if (!string.IsNullOrWhiteSpace(cleanedRoomNumber))
            {
                await PlayRoomNumberAsync(cleanedRoomNumber, audioRoot);
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

    private static async Task PlayRoomNumberAsync(string roomNumber, string voiceRoot)
    {
        var roomAudio = FindRoomAudio(roomNumber, voiceRoot);
        if (roomAudio is not null)
        {
            await PlayAudioFileAsync(roomAudio);
            return;
        }

        await SpeakTextAsync($"Room {roomNumber}");
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

    private static bool IsOromo(string? language) =>
        language?.Trim().Equals("Oromo", StringComparison.OrdinalIgnoreCase) == true
        || language?.Trim().Equals("Afaan Oromo", StringComparison.OrdinalIgnoreCase) == true;

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
