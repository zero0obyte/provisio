using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Provisio.Services;

/// <summary>One installed package with a newer version available.</summary>
public class PackageUpdate : INotifyPropertyChanged
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public required string Installed { get; init; }
    public required string Available { get; init; }
    public required string Source { get; init; }

    private bool _selected = true;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string Versions => $"{Installed}  ->  {Available}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Detects and installs updates for packages already on this PC, using winget.
/// winget prints a fixed-width table whose headers are localized, so rows are
/// parsed from the right (source, available, installed, id) and everything left
/// over is the package name.
/// </summary>
public static class PackageUpdateService
{
    /// <summary>The "------" rule winget prints between the header and the rows.</summary>
    private static readonly Regex DashRule = new(@"^\s*-{10,}\s*$", RegexOptions.Compiled);

    /// <summary>One header cell — used only for its start offset, so localized headers are fine.</summary>
    private static readonly Regex HeaderColumn = new(@"\S+(?: \S+)*", RegexOptions.Compiled);

    public static List<PackageUpdate> LastResult { get; private set; } = new();
    public static DateTime? LastChecked { get; private set; }
    public static bool IsChecking { get; private set; }

    /// <summary>Raised (on a background thread) whenever a check finishes.</summary>
    public static event EventHandler<List<PackageUpdate>>? UpdatesFound;

    public static bool WingetAvailable()
    {
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            foreach (var dir in paths)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    if (File.Exists(Path.Combine(dir.Trim(), "winget.exe"))) return true;
                }
                catch { /* malformed PATH entry */ }
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>Ask winget which installed packages have a newer version.</summary>
    public static async Task<List<PackageUpdate>> CheckAsync()
    {
        if (IsChecking) return LastResult;
        IsChecking = true;
        try
        {
            if (!WingetAvailable())
            {
                LastResult = new List<PackageUpdate>();
                LastChecked = DateTime.Now;
                return LastResult;
            }

            var (_, output) = await InstallEngine.CaptureAsync(
                "winget",
                "upgrade --include-unknown --disable-interactivity --accept-source-agreements");

            LastResult = Parse(output);
            LastChecked = DateTime.Now;
            UpdatesFound?.Invoke(null, LastResult);
            return LastResult;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// Parse winget's upgrade table.
    ///
    /// The columns are padded to fixed widths, so fields are sliced at the offsets taken from
    /// the header row rather than split on runs of spaces: package names legitimately contain
    /// double spaces ("Microsoft Visual C++ 2010  x86 Redistributable") and a full-width version
    /// leaves only a single space before the next column ("150.0.4078.105 151.0.4129.59").
    /// Both cases mis-parse with a whitespace split. Public so the rules stay testable by eye.
    /// </summary>
    public static List<PackageUpdate> Parse(string output)
    {
        var result = new List<PackageUpdate>();

        // winget draws a spinner with carriage returns before the table; keep the last
        // non-empty frame (a plain CRLF line ends with '\r', so "last segment" would be blank).
        var lines = (output ?? "").Split('\n')
            .Select(raw => raw.Split('\r').LastOrDefault(s => s.Trim().Length > 0)?.TrimEnd() ?? "")
            .ToList();

        var ruleIndex = lines.FindIndex(l => DashRule.IsMatch(l));
        if (ruleIndex < 1) return result;

        var starts = HeaderColumn.Matches(lines[ruleIndex - 1]).Select(m => m.Index).ToArray();
        if (starts.Length < 5) return result;   // unexpected layout — report nothing rather than nonsense

        for (var i = ruleIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0) break;   // table finished — later blocks are pinned/excluded packages
            if (line.Length <= starts[3]) continue;

            var name = Slice(line, starts, 0);
            var id = Slice(line, starts, 1);
            var installed = Slice(line, starts, 2);
            var available = Slice(line, starts, 3);
            var source = Slice(line, starts, 4);

            if (!source.Equals("winget", StringComparison.OrdinalIgnoreCase) &&
                !source.Equals("msstore", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Length == 0 || id.Length == 0 || available.Length == 0) continue;
            if (result.Any(p => p.Id == id)) continue;

            result.Add(new PackageUpdate
            {
                Name = name,
                Id = id,
                Installed = installed,
                Available = available,
                Source = source
            });
        }

        return result;
    }

    private static string Slice(string line, int[] starts, int column)
    {
        var start = starts[column];
        if (start >= line.Length) return "";
        var end = column < starts.Length - 1 ? Math.Min(starts[column + 1], line.Length) : line.Length;
        return line[start..end].Trim();
    }

    /// <summary>Upgrade one package. Returns true when winget reports success or "nothing to do".</summary>
    public static async Task<bool> UpgradeAsync(PackageUpdate package, Action<string> log)
    {
        log($"winget upgrade {package.Id}");
        var args = $"upgrade -e --id {package.Id} --source {package.Source} " +
                   "--silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
        var code = await InstallEngine.RunShellAsync("winget", args, log);
        return code == 0
               || code == unchecked((int)0x8A15002B)   // already installed / no applicable upgrade
               || code == unchecked((int)0x8A150056);
    }

    /// <summary>Silent "update everything" used by the launch-time auto-update option.</summary>
    public static async Task<int> UpgradeAllAsync(Action<string> log)
    {
        var updates = await CheckAsync();
        var done = 0;
        foreach (var package in updates)
        {
            if (await UpgradeAsync(package, log)) done++;
        }
        if (done > 0) LastResult = await CheckAsync();
        return done;
    }
}
