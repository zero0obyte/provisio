using System.ComponentModel;
using System.Runtime.CompilerServices;
using Provisio.Services;

namespace Provisio.Models;

public enum InstallStatus { Queued, Downloading, Installing, Done, Failed, Skipped }

/// <summary>A queued, runnable unit shown on the install progress screen.</summary>
public class InstallItem : INotifyPropertyChanged
{
    public required string KitName { get; init; }
    public required string ChoiceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required InstallAction Action { get; init; }

    private InstallStatus _status = InstallStatus.Queued;
    public InstallStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private string _detail = "";
    public string Detail
    {
        get => _detail;
        set { _detail = value; OnPropertyChanged(); }
    }

    public string StatusText => Status switch
    {
        InstallStatus.Queued => Loc.T("status.queued"),
        InstallStatus.Downloading => Loc.T("status.downloading"),
        InstallStatus.Installing => Loc.T("status.installing"),
        InstallStatus.Done => Loc.T("common.done"),
        InstallStatus.Failed => Loc.T("common.failed"),
        InstallStatus.Skipped => Loc.T("status.skipped"),
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
