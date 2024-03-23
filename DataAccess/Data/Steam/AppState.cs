using System.IO;

namespace Scum_Bag.DataAccess.Data.Steam;

internal sealed class AppState
{
    public string AppId { get; init; }
    public string LauncherPath { get; init; }
    public string Name { get; set; }
    public string InstallDir { get; init; }
    public string LibraryAppDir { get; set; }
    public string FullInstallDir { get { return Path.Combine(LibraryAppDir, InstallDir); } }
}
