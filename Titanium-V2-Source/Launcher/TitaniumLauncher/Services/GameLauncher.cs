using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using TitaniumLauncher.Models;
namespace TitaniumLauncher.Services;

public static class GameLauncher
{
    public const string ConsoleModuleName = "console.dll";
    private static readonly List<Process> Owned = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private static readonly Lazy<string> BundledRuntime = new(RuntimeBundle.Resolve);
    private static string Runtime => BundledRuntime.Value;
    public static bool IsRunning => Owned.Any(p => !p.HasExited);
    public static void ValidateBuild(BuildInfo build)
    {
        if (build.Source != DetectionSource.GameFiles || build.Version != "10.40" || build.Changelist != 9380822)
            throw new InvalidOperationException("Select Fortnite 10.40 CL-9380822. This Titanium package supports that build only.");
    }
    public static ProcessStartInfo CreateStartInfo(BuildInfo build, string name, bool host, string extra)
    {
        var start = new ProcessStartInfo(build.ExecutablePath) { WorkingDirectory = Path.GetDirectoryName(build.ExecutablePath)!, UseShellExecute = false,
            CreateNoWindow = host, WindowStyle = host ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal };
        // Match Reboot's compatibility setting for the game's older OpenSSL runtime.
        // Keep this scoped to our game processes rather than changing the machine.
        start.Environment["OPENSSL_ia32cap"] = "~0x20000000";
        // Use the local-account launch arguments published by Reboot's launcher.
        // Do not substitute guessed launcher-origin values.
        foreach (var arg in new[] { "-epicapp=Fortnite", "-epicenv=Prod", "-epiclocale=en-us", "-epicportal", "-AUTH_TYPE=epic", "-AUTH_LOGIN=" + name + "@projectreboot.dev",
            "-AUTH_PASSWORD=Rebooted", "-skippatchcheck", "-nobe", "-fromfl=eac", "-fltoken=3db3ba5dcbd2e16703f3978d",
            "-caldera=eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJhY2NvdW50X2lkIjoiYmU5ZGE1YzJmYmVhNDQwN2IyZjQwZWJhYWQ4NTlhZDQiLCJnZW5lcmF0ZWQiOjE2Mzg3MTcyNzgsImNhbGRlcmFHdWlkIjoiMzgxMGI4NjMtMmE2NS00NDU3LTliNTgtNGRhYjNiNDgyYTg2IiwiYWNQcm92aWRlciI6IkVhc3lBbnRpQ2hlYXQiLCJub3RlcyI6IiIsImZhbGxiYWNrIjpmYWxzZX0.VAWQB67RTxhiWOxx7DBjnzDnXyyEnX7OljJm-j2d88G_WgwQ9wrE6lwMEHZHjBd1ISJdUO1UVUqkfLdU5nofBQ" }) start.ArgumentList.Add(arg);
        if (host) foreach (var arg in new[] { "-windowed", "-ResX=640", "-ResY=360", "-nosound", "-nosplash", "-unattended", "-ExecCmds=t.IdleWhenNotForeground 0" }) start.ArgumentList.Add(arg);
        else foreach (var arg in SplitArguments(extra))
        {
            if (arg.StartsWith("-AUTH_", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-epic", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-ExecCmds", StringComparison.OrdinalIgnoreCase) || arg.Equals("-nullrhi", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Extra arguments cannot replace managed startup settings or hide the player window.");
            start.ArgumentList.Add(arg);
        }
        return start;
    }
    public static IEnumerable<string> SplitArguments(string text)
    {
        var token = new System.Text.StringBuilder(); bool quoted = false;
        foreach (char ch in text ?? "")
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(ch) && !quoted) { if (token.Length > 0) { yield return token.ToString(); token.Clear(); } }
            else token.Append(ch);
        }
        if (quoted) throw new InvalidOperationException("An extra launch argument has an unclosed quote.");
        if (token.Length > 0) yield return token.ToString();
    }
    private static void VerifyModules()
    {
        Dictionary<string, string>? hashes;
        try { hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Runtime, "dlls", "manifest.json"))); }
        catch (JsonException ex) { throw new InvalidOperationException("Titanium's file manifest is damaged. Extract the complete launcher archive again.", ex); }
        if (hashes is null) throw new InvalidOperationException("Titanium's file manifest is empty. Extract the complete launcher archive again.");
        foreach (var name in ModuleStartup.RequiredModules)
        {
            var file = Path.Combine(Runtime, "dlls", name);
            if (!File.Exists(file) || !hashes.TryGetValue(name, out var expected) || !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bundled module is missing or changed: " + name + ". Extract the complete launcher ZIP again.");
        }
    }
    public static string CheckRuntime() { VerifyModules(); return Runtime; }
    private static Process StartOwned(ProcessStartInfo info)
    {
        var process = Process.Start(info) ?? throw new InvalidOperationException("Windows could not start " + Path.GetFileName(info.FileName));
        Owned.Add(process); return process;
    }
    private static async Task StartBackendAsync(Profile profile,int season)
    {
        var occupied = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().FirstOrDefault(e => e.Port is 3551 or 80);
        if (occupied is not null)
            throw new InvalidOperationException("Port " + occupied.Port + " is in use. Close the other Reboot/backend instance, then press Play again.");
        var destination = Path.Combine(SettingsService.DataDirectory, "Backend");
        var profileDirectory=BackendProfiles.Prepare(Path.Combine(Runtime,"backend"),destination,profile,season);
        var backend = StartOwned(new ProcessStartInfo(Path.Combine(destination, "lawinserver.exe"))
        { WorkingDirectory = profileDirectory, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (backend.HasExited) throw new InvalidOperationException("The local Reboot backend exited during startup.");
            try { using var response = await Http.GetAsync("http://127.0.0.1:3551/fortnite/api/version"); if (response.IsSuccessStatusCode) return; }
            catch (HttpRequestException) { } catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("The local backend did not become ready within 30 seconds.");
    }
    public static async Task LaunchAsync(BuildInfo build, Profile profile, int season, string extraArguments, Action<string> status, MatchOptions? options = null, bool enableMenu = false)
    {
        ValidateBuild(build);
        if (IsRunning) throw new InvalidOperationException("This launcher already has a running game. Close it before starting another session.");
        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(build.ExecutablePath)).Any())
            throw new InvalidOperationException("Close the running Fortnite client/server before starting a new Titanium session.");
        await Task.Run(VerifyModules);
        CompanionProcesses.Validate(build);
        var clientInfo = CreateStartInfo(build, profile.Name, false, extraArguments);
        var hostInfo = CreateStartInfo(build, "TitaniumHost", true, "");
        options ??= MatchOptions.Parse("",true);
        options.Apply(hostInfo,true);options.Apply(clientInfo,false);
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(e => e.Port == 7777))
            throw new InvalidOperationException("Port 7777 is already in use by another game server. Close that server first.");
        Directory.CreateDirectory(SettingsService.DataDirectory);
        var sessionDirectory = Path.Combine(SettingsService.DataDirectory, "Logs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sessionDirectory);
        var hostLog = Path.Combine(sessionDirectory, "host.log");
        var clientLog = Path.Combine(sessionDirectory, "client.log");
        Process? activeGame = null;
        var activeLog = hostLog;
        hostInfo.ArgumentList.Add("-abslog=" + hostLog);
        clientInfo.ArgumentList.Add("-abslog=" + clientLog);
        void Record(string message) => File.AppendAllText(Path.Combine(sessionDirectory, "startup.log"), DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
        async Task Initialize(Process process, bool isHost, string log)
        {
            var role = isHost ? "server" : "player";
            Record(role + " PID " + process.Id);
            await ModuleStartup.InitializeAsync(isHost, enableMenu, async name =>
            {
                Record(role + " loading " + name);
                await NativeLoader.LoadAsync(process, Path.Combine(Runtime, "dlls", name), message => { Record(role + " " + message); status(message); });
                Record(role + " loaded " + name);
            }, () => StartupReadiness.WaitForEngineAsync(process, log, status), () => StartupReadiness.WaitAsync(process, log, status));
        }
        File.WriteAllText(Path.Combine(SettingsService.DataDirectory, "launch.json"), JsonSerializer.Serialize(new
        { profile = profile.Name, profileMode = profile.Mode.ToString(), season, buildVersion = build.Version, changelist = build.Changelist, serverBuild = "ResourceMovementFix", launcherVersion = "1.1.18", menuEnabled = enableMenu }));
        try
        {
            Record("Launcher 1.1.18; Fortnite " + build.Version + " CL-" + build.Changelist);
            status("Checking Fortnite's content files...");
            GameInstallation.Validate(build, Runtime, Record);
            status("Starting the local login backend..."); await StartBackendAsync(profile,season);
            status("Starting the Titanium 10.40 server...");
            CompanionProcesses.Start(build, process => { Owned.Add(process); Record("server companion " + process.ProcessName + " PID " + process.Id); });
            var host = StartOwned(hostInfo); activeGame = host;
            _ = ServerWindowGuard.KeepHiddenAsync(host, System.Threading.CancellationToken.None);
            await Initialize(host, true, hostLog);
            status("Titanium is loading the map. Waiting for port 7777...");
            var waiting = Stopwatch.StartNew();
            while (!ServerPort.IsOwnedBy(host.Id, 7777))
            {
                if (host.HasExited) throw new InvalidOperationException("The 10.40 game server exited while loading (0x" + host.ExitCode.ToString("X8") + "). Logs: " + sessionDirectory);
                if (waiting.Elapsed > TimeSpan.FromSeconds(180)) throw new TimeoutException("The 10.40 server did not open port 7777 within three minutes. Logs: " + sessionDirectory);
                await Task.Delay(500);
            }
            await StartupReadiness.SaveLatestAsync(host, hostLog);
            status("Starting Fortnite 10.40...");
            CompanionProcesses.Start(build, process => { Owned.Add(process); Record("player companion " + process.ProcessName + " PID " + process.Id); });
            var client = StartOwned(clientInfo); activeGame = client; activeLog = clientLog;
            await Initialize(client, false, clientLog);
            status("Ready. Press Play in the main lobby to join your local match.");
            _ = ObserveSessionAsync(client, host, status, Record, clientLog);
        }
        catch (Exception error)
        {
            if (activeGame is not null) await StartupReadiness.SaveLatestAsync(activeGame, activeLog);
            Record("Failed: " + error); StopOwned();
            var diagnosis = StartupFailure.ReadDiagnosis(activeLog);
            throw new InvalidOperationException((diagnosis ?? error.Message) + " Logs: " + sessionDirectory, error);
        }
    }
    private static async Task ObserveSessionAsync(Process client, Process host, Action<string> status, Action<string> record, string clientLog)
    {
        try
        {
            await Task.WhenAny(client.WaitForExitAsync(), host.WaitForExitAsync());
            var exited = client.HasExited ? client : host;
            var role = client.HasExited ? "Player" : "Server";
            record(role + " exited: 0x" + exited.ExitCode.ToString("X8"));
            await StartupReadiness.SaveLatestAsync(client, clientLog);
            var message = exited.ExitCode == 0 ? role + " closed. Press Play to start a fresh session." : role + " exited (0x" + exited.ExitCode.ToString("X8") + "). Logs: " + Path.GetDirectoryName(clientLog);
            // Fortnite can send the last locker/profile mutation while the client is closing.
            // Keep the backend alive briefly so its synchronous profile write reaches disk.
            record("Waiting for the local backend to flush the player profile.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            StopOwned(); status(message);
        }
        catch (Exception) { /* Launcher shutdown owns cleanup. */ }
    }
    public static void StopOwned()
    {
        foreach (var process in Owned.ToArray().Reverse())
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        }
        Owned.Clear();
    }
}
