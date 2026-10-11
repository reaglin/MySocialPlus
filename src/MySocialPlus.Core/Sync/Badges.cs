using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Sync;

/// <summary>
/// The number on each node: what is new beneath it since Ron last opened it, summed up the tree
/// (rule 3). An account the program cannot read never has one.
/// </summary>
public static class Badges
{
    /// <summary>Badge per node id, for every node in the tree (0 when nothing is new).</summary>
    public static Dictionary<string, int> Compute(SocialTree tree, IReadOnlyDictionary<string, int> unseenByAccount)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var root in tree.Roots) Sum(root);
        return result;

        int Sum(TreeNode n)
        {
            var own = n.Account is { IsReadable: true } a && unseenByAccount.TryGetValue(a.Key, out var c) ? c : 0;
            var total = own + n.Children.Sum(Sum);
            result[n.Id] = total;
            return total;
        }
    }
}
