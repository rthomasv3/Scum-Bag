using System.Collections.Generic;
using System.Text.Json.Serialization;
using Scum_Bag.DataAccess.Data;

namespace Scum_Bag;

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(IEnumerable<SaveGame>))]
[JsonSerializable(typeof(List<SaveGame>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SaveDataJsonSerializerContext : JsonSerializerContext
{

}
