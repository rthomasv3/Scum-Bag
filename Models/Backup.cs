using System;

namespace Scum_Bag.Models;

internal sealed class Backup
{
    public Guid SaveId { get; set; }
    public long Time { get; set; }
    public string Directory { get; set; }
    public string Tag { get; set; }
    public bool IsFavorite { get; set; }
}