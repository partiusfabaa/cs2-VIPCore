namespace VIPCore.Configs;

public class GroupsConfig : Dictionary<string, VipGroup>
{
    public Dictionary<string, object> ResolveGroup(string groupName)
    {
        return ResolveGroupInternal(groupName, []);
    }

    private Dictionary<string, object> ResolveGroupInternal(string groupName, HashSet<string> visited)
    {
        if (!ContainsKey(groupName))
            throw new KeyNotFoundException($"Group {groupName} not found");

        if (!visited.Add(groupName))
            throw new InvalidOperationException($"Cyclic inheritance detected in group {groupName}");

        var group = this[groupName];
        var resolved = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(group.Inherits))
        {
            var parent = ResolveGroupInternal(group.Inherits, new HashSet<string>(visited));
            foreach (var kvp in parent)
            {
                resolved[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in group.Where(kvp => kvp.Key != "$inherit"))
        {
            resolved[kvp.Key] = kvp.Value;
        }

        return resolved;
    }
}

public class VipGroup : Dictionary<string, object>
{
    public string? Inherits => TryGetValue("$inherit", out var inherit) ? inherit.ToString() : null;
}