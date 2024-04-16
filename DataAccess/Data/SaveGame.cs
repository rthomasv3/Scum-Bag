using System;
using System.Collections.Generic;

namespace Scum_Bag.DataAccess.Data;

internal sealed class SaveGame
{
    public Guid Id { get; set; }
    public bool Enabled { get; set; }
    public string BackupLocation { get; set; }
    
    public string Name { get; init; }
    public string SaveLocation { get; init; }
    public int Frequency { get; init; }
    public int MaxBackups { get; init; }
    public string Game { get; init; }
    public string Icon { get; init; }
    public Dictionary<string, BackupMetadata> BackupMetadata { get; set; } = new Dictionary<string, BackupMetadata>();
}
