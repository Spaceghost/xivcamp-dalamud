using System.Text.Json;
namespace XivCamp.Core;
public static class HostOwnership
{
    public static bool ExternalScreenOwner(string owner,JsonElement root)
    {
        var key=owner switch{"chat"=>"XivHud.Chat","combat"=>"XivHud.Combat.Deck","inventory"=>"XivHud.Inventory","crafting"=>"XivHud.Crafting",_=>null};
        if(key is null)return false;
        if(root.TryGetProperty("result",out var result))root=result;
        if(!root.TryGetProperty("panels",out var panels)||panels.ValueKind!=JsonValueKind.Array)throw new ArgumentException("Unknown host snapshot");
        foreach(var panel in panels.EnumerateArray().Take(64))
        {
            if(!panel.TryGetProperty("kind",out var kind)||kind.GetString()!="adopted")continue;
            if(!panel.TryGetProperty("app",out var app)||app.ValueKind!=JsonValueKind.String)continue;
            var name=app.GetString()!;var marker=name.LastIndexOf("###",StringComparison.Ordinal);
            if((marker>=0?name[(marker+3)..]:name)==key)return true;
        }
        return false;
    }
}
