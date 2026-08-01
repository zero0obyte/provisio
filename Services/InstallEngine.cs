using System.Diagnostics;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using Provisio.Models;

namespace Provisio.Services;

/// <summary>
/// Runs the queued install batch: winget installs and upgrades, downloaded installers
/// (cached), MSIX/APPX bundles, shell commands and PowerShell tweaks.
/// Failures are logged and the queue continues.
/// </summary>
public static class InstallEngine
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>Package formats that Windows installs through Add-AppxPackage rather than an installer .exe.</summary>
    public static readonly string[] MsixExtensions = { ".msix", ".msixbundle", ".appx", ".appxbundle" };

    /// <summary>Everything a "download" action may point at, for the Kit Editor hint.</summary>
    public static readonly string[] DownloadExtensions = { ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle" };

    public static bool IsAdmin
    {
        get
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    public static bool IsMsix(string target)
    {
        try
        {
            var path = target.Contains("://") ? new Uri(target).LocalPath : target;
            var ext = Path.GetExtension(path);
            return MsixExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string WingetArgs(InstallAction a) =>
        $"install -e --id {a.Target}" +
        (string.IsNullOrWhiteSpace(a.Source) ? "" : $" --source {a.Source}") +
        " --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";

    /// <summary>Human-readable command for dry-run preview.</summary>
    public static string DescribeCommand(InstallAction a) => a.Type switch
    {
        "winget" => $"winget {WingetArgs(a)}",
        "winget-upgrade" => $"winget upgrade -e --id {a.Target} --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
        "download" => IsMsix(a.Target)
            ? $"download {a.Target}  ->  Add-AppxPackage"
            : $"download {a.Target}  ->  run with: {a.Args}",
        "msix" => $"Add-AppxPackage {a.Target}",
        "shell" => $"cmd /c {Combine(a.Target, a.Args)}",
        "script" => $"powershell -NoProfile -ExecutionPolicy Bypass -Command \"{a.Target}\"",
        _ => "(no action)"
    };

    private static string Combine(string target, string args) =>
        string.IsNullOrWhiteSpace(args) ? target : $"{target} {args}";

    /// <summary>Relaunch the app elevated. Returns false if the user declined UAC.</summary>
    public static bool RestartAsAdmin()
    {
        try
        {
            var exe = Environment.ProcessPath
                      ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (exe is null) return false;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            Environment.Exit(0);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Create a System Restore point (best-effort, needs admin).</summary>
    public static async Task<string> CreateRestorePointAsync(Action<string> log)
    {
        log("Enabling System Restore on C: (best-effort)…");
        await RunShellAsync("powershell",
            "-NoProfile -ExecutionPolicy Bypass -Command \"Enable-ComputerRestore -Drive 'C:\\'\"",
            log);
        log("Creating restore point 'Provisio Setup'…");
        var code = await RunShellAsync("powershell",
            "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'Provisio Setup' -RestorePointType 'MODIFY_SETTINGS'\"",
            log);
        var msg = code == 0
            ? "Restore point created."
            : "Restore point could not be created (needs admin, or one was already created in the last 24h).";
        log(msg);
        return msg;
    }

    /// <summary>Run one queued item; returns true on success. Never throws.</summary>
    public static async Task<bool> RunItemAsync(InstallItem item, Action<string> log)
    {
        var a = item.Action;
        try
        {
            switch (a.Type)
            {
                case "winget":
                {
                    item.Status = InstallStatus.Installing;
                    log($"[{item.Name}] winget install {a.Target}");
                    var code = await RunShellAsync("winget", WingetArgs(a), log);
                    // winget returns non-zero when already installed (-1978335189) — treat as success.
                    if (code == 0 || code == unchecked((int)0x8A15002B))
                    {
                        item.Status = InstallStatus.Done;
                        item.Detail = code == 0 ? "Installed." : "Already installed.";
                        return true;
                    }
                    item.Status = InstallStatus.Failed;
                    item.Detail = $"winget exited with code {code}.";
                    return false;
                }

                case "winget-upgrade":
                {
                    item.Status = InstallStatus.Installing;
                    log($"[{item.Name}] winget upgrade {a.Target}");
                    var args = $"upgrade -e --id {a.Target}" +
                               (string.IsNullOrWhiteSpace(a.Source) ? "" : $" --source {a.Source}") +
                               " --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
                    var code = await RunShellAsync("winget", args, log);
                    if (code == 0)
                    {
                        item.Status = InstallStatus.Done;
                        item.Detail = "Updated.";
                        return true;
                    }
                    // "no applicable upgrade found" — nothing to do, not an error.
                    if (code == unchecked((int)0x8A15002B) || code == unchecked((int)0x8A150056))
                    {
                        item.Status = InstallStatus.Done;
                        item.Detail = "Already up to date.";
                        return true;
                    }
                    item.Status = InstallStatus.Failed;
                    item.Detail = $"winget exited with code {code}.";
                    return false;
                }

                case "download":
                {
                    item.Status = InstallStatus.Downloading;
                    var file = await DownloadAsync(a.Target, log);
                    item.Status = InstallStatus.Installing;

                    if (IsMsix(file))
                        return await InstallMsixAsync(item, file, log);

                    log($"[{item.Name}] running {Path.GetFileName(file)} {a.Args}");
                    var dcode = await RunShellAsync(file, a.Args, log);
                    if (dcode == 0)
                    {
                        item.Status = InstallStatus.Done;
                        item.Detail = "Installed.";
                        return true;
                    }
                    item.Status = InstallStatus.Failed;
                    item.Detail = $"Installer exited with code {dcode}.";
                    return false;
                }

                case "msix":
                {
                    // A local path is used as-is; a URL is downloaded to the installer cache first.
                    string file;
                    if (a.Target.Contains("://"))
                    {
                        item.Status = InstallStatus.Downloading;
                        file = await DownloadAsync(a.Target, log);
                    }
                    else
                    {
                        file = a.Target;
                        if (!File.Exists(file))
                        {
                            item.Status = InstallStatus.Failed;
                            item.Detail = "MSIX bundle not found.";
                            return false;
                        }
                    }
                    item.Status = InstallStatus.Installing;
                    return await InstallMsixAsync(item, file, log);
                }

                case "shell":
                {
                    item.Status = InstallStatus.Installing;
                    var command = Combine(a.Target, a.Args);
                    log($"[{item.Name}] cmd /c {command}");
                    var code = await RunShellAsync("cmd.exe", $"/d /c {command}", log);
                    if (code == 0)
                    {
                        item.Status = InstallStatus.Done;
                        item.Detail = "Command completed.";
                        return true;
                    }
                    item.Status = InstallStatus.Failed;
                    item.Detail = $"Command exited with code {code}.";
                    return false;
                }

                case "script":
                {
                    item.Status = InstallStatus.Installing;
                    log($"[{item.Name}] {a.Target}");
                    var scode = await RunShellAsync("powershell",
                        $"-NoProfile -ExecutionPolicy Bypass -Command \"{a.Target.Replace("\"", "\\\"")}\"",
                        log);
                    if (scode == 0)
                    {
                        item.Status = InstallStatus.Done;
                        item.Detail = "Applied.";
                        return true;
                    }
                    item.Status = InstallStatus.Failed;
                    item.Detail = $"Script exited with code {scode}.";
                    return false;
                }

                default:
                    item.Status = InstallStatus.Skipped;
                    item.Detail = "No action defined.";
                    return true;
            }
        }
        catch (Exception ex)
        {
            item.Status = InstallStatus.Failed;
            item.Detail = ex.Message;
            log($"[{item.Name}] ERROR: {ex.Message}");
            return false;
        }
    }

    /// <summary>Install a downloaded .msix/.msixbundle/.appx/.appxbundle package.</summary>
    private static async Task<bool> InstallMsixAsync(InstallItem item, string file, Action<string> log)
    {
        log($"[{item.Name}] Add-AppxPackage {Path.GetFileName(file)}");
        var code = await RunShellAsync("powershell",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{file.Replace("'", "''")}' -ForceApplicationShutdown\"",
            log);
        if (code == 0)
        {
            item.Status = InstallStatus.Done;
            item.Detail = "Installed (MSIX).";
            return true;
        }
        item.Status = InstallStatus.Failed;
        item.Detail = $"Add-AppxPackage exited with code {code}. The bundle may be unsigned or already installed.";
        return false;
    }

    /// <summary>Download to the local cache; re-use the cached file when present (offline mode).</summary>
    private static async Task<string> DownloadAsync(string url, Action<string> log)
    {
        Directory.CreateDirectory(SettingsService.InstallerCacheFolder);
        var name = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrWhiteSpace(name)) name = Guid.NewGuid() + ".exe";
        var path = Path.Combine(SettingsService.InstallerCacheFolder, name);

        if (SettingsService.CacheInstallers && File.Exists(path))
        {
            log($"Using cached installer: {name}");
            return path;
        }

        log($"Downloading {url}");
        await using (var stream = await Http.GetStreamAsync(url))
        await using (var fs = File.Create(path))
        {
            await stream.CopyToAsync(fs);
        }
        return path;
    }

    /// <summary>Run a process, streaming its output to the log. Returns the exit code.</summary>
    public static async Task<int> RunShellAsync(string file, string args, Action<string> log)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;

        async Task Pump(StreamReader reader)
        {
            string? line;
            int count = 0;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                // Keep the log useful but bounded: first ~40 lines per process.
                if (count++ < 40 && !string.IsNullOrWhiteSpace(line)) log("    " + line.TrimEnd());
            }
        }

        var t1 = Pump(proc.StandardOutput);
        var t2 = Pump(proc.StandardError);
        await proc.WaitForExitAsync();
        await Task.WhenAll(t1, t2);
        return proc.ExitCode;
    }

    /// <summary>Run a process and capture its full stdout (used to parse winget output).</summary>
    public static async Task<(int code, string output)> CaptureAsync(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return (proc.ExitCode, stdout + stderr);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
