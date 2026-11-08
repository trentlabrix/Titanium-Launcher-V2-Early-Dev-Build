using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace TitaniumLauncher.Services;

public static class StartupReadiness
{
    private static string StandardLogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Logs");

    public static bool IsCurrentStartup(string firstLine, DateTime startedUtc)
    {
        const string prefix = "Log file open, ";
        return firstLine.StartsWith(prefix, StringComparison.Ordinal) &&
            DateTime.TryParseExact(firstLine[prefix.Length..].Trim(), new[] { "MM/dd/yy HH:mm:ss", "M/d/yy HH:mm:ss" },
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var opened) &&
            opened.ToUniversalTime() >= startedUtc.AddSeconds(-1);
    }

    private static string? FindLog(string requested, DateTime startedUtc)
    {
        // Concurrent Fortnite processes use FortniteGame.log, FortniteGame_2.log,
        // etc. A server writing its old log must never satisfy player readiness.
        var standard = Directory.Exists(StandardLogDirectory)
            ? Directory.EnumerateFiles(StandardLogDirectory, "FortniteGame*.log")
                .Where(p => !Path.GetFileName(p).Contains("-backup-", StringComparison.OrdinalIgnoreCase))
            : Enumerable.Empty<string>();
        foreach (var candidate in standard.Append(requested).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(candidate) < startedUtc) continue;
                using var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                if (IsCurrentStartup(reader.ReadLine() ?? "", startedUtc)) return candidate;
            }
            catch (IOException) { }
        }
        return null;
    }

    public static async Task SaveLatestAsync(Process process, string destination)
    {
        try
        {
            var source = FindLog(destination, process.StartTime.ToUniversalTime());
            if (source is null || source.Equals(destination, StringComparison.OrdinalIgnoreCase)) return;
            using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            await File.WriteAllTextAsync(destination, await reader.ReadToEndAsync());
        }
        catch (IOException) { }
        catch (InvalidOperationException) { }
        catch (UnauthorizedAccessException) { }
    }
    public static bool IsEngineReady(string log) => log.Contains("(Engine Initialization) Total time:", StringComparison.Ordinal);

    public static bool IsReady(string log) =>
        IsEngineReady(log) &&
        log.Split('\n').Any(line => line.Contains("[UOnlineAccountCommon::ContinueLoggingIn]", StringComparison.Ordinal) &&
            line.Contains("(Completed)", StringComparison.Ordinal));

    public static Task WaitAsync(Process process, string logPath, Action<string> status) => WaitAsync(process, logPath, status, false);
    public static Task WaitForEngineAsync(Process process, string logPath, Action<string> status) => WaitAsync(process, logPath, status, true);

    private static async Task WaitAsync(Process process, string logPath, Action<string> status, bool engineOnly)
    {
        var timer = Stopwatch.StartNew();
        var started = process.StartTime.ToUniversalTime();
        while (timer.Elapsed < TimeSpan.FromSeconds(150))
        {
            if (process.HasExited)
            {
                await SaveLatestAsync(process, logPath);
                throw new InvalidOperationException(StartupFailure.ReadDiagnosis(logPath) ??
                    ("Fortnite exited during startup (0x" + process.ExitCode.ToString("X8") + "). Log: " + logPath));
            }
            try
            {
                // Some shipping builds ignore -abslog. Accept their standard log only
                // when it was written by a new startup, and retain a session snapshot.
                var activeLog = FindLog(logPath, started);
                if (activeLog is not null)
                {
                    using var stream = new FileStream(activeLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    var contents = await reader.ReadToEndAsync();
                    if (engineOnly ? IsEngineReady(contents) : IsReady(contents))
                    {
                        if (activeLog != logPath) await File.WriteAllTextAsync(logPath, contents);
                        return;
                    }
                }
            }
            catch (IOException) { /* The game has not created/flushed its log yet. */ }
            status((engineOnly ? "Initializing Fortnite's engine... " : "Waiting for local login... ") + (int)timer.Elapsed.TotalSeconds + "s");
            await Task.Delay(500);
        }
        throw new TimeoutException("Fortnite did not finish engine startup and local login. Log: " + logPath);
    }
}
