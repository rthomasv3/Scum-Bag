using System.Collections.Generic;

namespace Scum_Bag.DataAccess.Data;

internal sealed class TreeNode
{
    public string key { get; set; }
    public string label { get; set; }
    public string type { get; set; }
    public List<TreeNode> children { get; set; } = new();
}