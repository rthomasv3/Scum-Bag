using System.Collections.Generic;

namespace Scum_Bag.DataAccess.Data.Steam;

internal sealed class LibraryFolders<TKey, TValue> : Dictionary<TKey, TValue>
{
    public string ContentStatsId { get; init; }
}
