using System.Text.Json;
using System.Text.Json.Nodes;

namespace VIPCore.Configs;

public static class LegacyConfigMigration
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static void Run()
    {
        var groupsPath = Path.Combine(ConfigSystem.CoreConfigDirectory, "vip.json");
        var corePath = Path.Combine(ConfigSystem.CoreConfigDirectory, "vip_core.json");

        if (!File.Exists(groupsPath)) return;

        if (JsonNode.Parse(File.ReadAllText(groupsPath), documentOptions: DocumentOptions) is not JsonObject root ||
            root["Groups"] is not JsonObject legacyGroups)
            return;

        var groups = new JsonObject();
        foreach (var (name, group) in legacyGroups)
        {
            var values = group is JsonObject obj && obj["Values"] is JsonObject inner ? inner : group;
            groups[name] = values?.DeepClone();
        }

        Backup(groupsPath);
        File.WriteAllText(groupsPath, groups.ToJsonString(ConfigSystem.ConfigJsonOptions));

        if (!File.Exists(corePath) ||
            JsonNode.Parse(File.ReadAllText(corePath), documentOptions: DocumentOptions) is not JsonObject core)
            return;

        var changed = false;

        if (root["Delay"] is { } delay && core["Delay"] is null)
        {
            core["Delay"] = delay.DeepClone();
            changed = true;
        }

        if (core["UseCenterHtmlMenu"] is { } useCenterHtmlMenu)
        {
            core.Remove("UseCenterHtmlMenu");
            core["MenuType"] ??= useCenterHtmlMenu.GetValue<bool>() ? "center" : "chat";
            changed = true;
        }

        if (!changed) return;

        Backup(corePath);
        File.WriteAllText(corePath, core.ToJsonString(ConfigSystem.ConfigJsonOptions));
    }

    private static void Backup(string path)
    {
        File.Copy(path, path + ".old", overwrite: true);
    }
}
