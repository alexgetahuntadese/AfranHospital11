using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text.RegularExpressions;

namespace AfranHospitalKiosk;

public sealed partial class AmharicTicketAnnouncer
{
    private readonly SemaphoreSlim _speechLock = new(1, 1);
    private readonly string _root;
    private readonly string _pythonExe;
    private readonly string _scriptPath;
    private readonly string _modelDir;
    private readonly string _audioDir;

    public AmharicTicketAnnouncer()
    {
        _root = FindVoiceRoot();
        _pythonExe = Path.Combine(_root, ".venv", "Scripts", "python.exe");
        _scriptPath = Path.Combine(_root, "synthesize_ticket.py");
        _modelDir = Path.Combine(_root, "model");
        _audioDir = Path.Combine(_root, "generated");
    }

    public bool IsReady => File.Exists(_pythonExe) && File.Exists(_scriptPath);

    public async Task AnnounceAsync(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket == "-" || !IsReady)
        {
            return;
        }

        await _speechLock.WaitAsync();
        try
        {
            var safeTicket = SafeFilePart(ticket);
            var outputPath = Path.Combine(_audioDir, $"{safeTicket}.wav");
            if (!IsPcmWave(outputPath))
            {
                await GenerateAudioAsync(ticket, outputPath);
            }

            using var player = new SoundPlayer(outputPath);
            player.PlaySync();
        }
        catch
        {
            // The queue display must never fail because optional voice playback failed.
        }
        finally
        {
            _speechLock.Release();
        }
    }

    private async Task GenerateAudioAsync(string ticket, string outputPath)
    {
        Directory.CreateDirectory(_audioDir);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.StartInfo.ArgumentList.Add(_scriptPath);
        process.StartInfo.ArgumentList.Add("--text");
        process.StartInfo.ArgumentList.Add(BuildAnnouncementText(ticket));
        process.StartInfo.ArgumentList.Add("--output");
        process.StartInfo.ArgumentList.Add(outputPath);
        process.StartInfo.ArgumentList.Add("--model-dir");
        process.StartInfo.ArgumentList.Add(_modelDir);
        process.StartInfo.ArgumentList.Add("--offline");

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || !IsPcmWave(outputPath))
        {
            throw new InvalidOperationException("Amharic TTS generation failed.");
        }
    }

    private static string BuildAnnouncementText(string ticket)
    {
        var match = TicketPattern().Match(ticket.Trim());
        if (!match.Success)
        {
            return $"ibakwo kutir {ticket} wede memezgebiya kotari hulet yihidu";
        }

        var prefix = match.Groups["prefix"].Value.Equals("F", StringComparison.OrdinalIgnoreCase) ? "ef" : "em";
        var number = int.Parse(match.Groups["number"].Value);
        return $"ibakwo kutir {prefix} {NumberToAmharicLatin(number)} wede memezgebiya kotari hulet yihidu";
    }

    private static string NumberToAmharicLatin(int number)
    {
        if (number <= 0)
        {
            return "zero";
        }

        if (number < 100)
        {
            return NumberBelowHundred(number);
        }

        var hundreds = number / 100;
        var remainder = number % 100;
        var hundredText = hundreds == 1 ? "meto" : $"{NumberBelowHundred(hundreds)} meto";

        return remainder == 0 ? hundredText : $"{hundredText} {NumberBelowHundred(remainder)}";
    }

    private static string NumberBelowHundred(int number)
    {
        string[] ones =
        [
            "",
            "and",
            "hulet",
            "sost",
            "arat",
            "amist",
            "sidist",
            "sebat",
            "simint",
            "zetegn"
        ];

        string[] tens =
        [
            "",
            "asir",
            "haya",
            "selasa",
            "arba",
            "hamsa",
            "silsa",
            "seba",
            "semanya",
            "zetena"
        ];

        if (number < 10)
        {
            return ones[number];
        }

        if (number == 10)
        {
            return "asir";
        }

        var ten = number / 10;
        var one = number % 10;
        return one == 0 ? tens[ten] : $"{tens[ten]} {ones[one]}";
    }

    private static string FindVoiceRoot()
    {
        var candidates = FindCandidateRoots().ToList();

        var readyPath = candidates.FirstOrDefault(path =>
            File.Exists(Path.Combine(path, ".venv", "Scripts", "python.exe"))
            && File.Exists(Path.Combine(path, "synthesize_ticket.py")));
        if (readyPath is not null)
        {
            return readyPath;
        }

        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "synthesize_ticket.py")))
            ?? Path.Combine(AppContext.BaseDirectory, "Tools", "AmharicTts");
    }

    private static IEnumerable<string> FindCandidateRoots()
    {
        foreach (var basePath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(basePath);
            while (directory is not null)
            {
                yield return Path.Combine(directory.FullName, "Tools", "AmharicTts");
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

    private static bool IsPcmWave(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 44
            || new string(reader.ReadChars(4)) != "RIFF")
        {
            return false;
        }

        stream.Position = 8;
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            return false;
        }

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();
            if (chunkId == "fmt ")
            {
                var audioFormat = reader.ReadInt16();
                return audioFormat == 1;
            }

            stream.Position += chunkSize;
        }

        return false;
    }

    [GeneratedRegex("^(?<prefix>[A-Za-z]+)(?<number>\\d+)$")]
    private static partial Regex TicketPattern();
}
