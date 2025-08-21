using System;
using System.IO;

namespace Scum_Bag.Models;

internal sealed class WatchLocation
{
    public FileSystemWatcher Watcher { get; set; }
    public Guid SaveGameId { get; set; }
    public string Location { get; set; }
    public string GameDirectory { get; set; }
    public bool IsTakingScreenshot { get; set; }
}