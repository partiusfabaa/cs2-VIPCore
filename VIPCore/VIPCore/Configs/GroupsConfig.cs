namespace VIPCore.Configs;

public class GroupsConfig : Dictionary<string, VipGroup>
{
    public Dictionary<string, object>? ResolveGroup(string groupName)
    {
        return ContainsKey(groupName) ? ResolveGroupInternal(groupName, []) : null;
    }

    private Dictionary<string, object> ResolveGroupInternal(string groupName, HashSet<string> visited)
    {
        if (!TryGetValue(groupName, out var group))
            throw new KeyNotFoundException($"Group '{groupName}' not found");

        if (!visited.Add(groupName))
            throw new InvalidOperationException($"Cyclic inheritance detected in group '{groupName}'");

        var resolved = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(group.Inherits))
        {
            foreach (var (key, value) in ResolveGroupInternal(group.Inherits, visited))
                resolved[key] = value;
        }

        foreach (var (key, value) in group.Where(kvp => kvp.Key != VipGroup.InheritKey))
            resolved[key] = value;

        return resolved;
    }
}

public class VipGroup : Dictionary<string, object>
{
    public const string InheritKey = "$inherit";

    public string? Inherits => TryGetValue(InheritKey, out var inherit) ? inherit.ToString() : null;
}
