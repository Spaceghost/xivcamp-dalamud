using System.Text.Json;
using Dalamud.Plugin;
using XivCamp.Core;
namespace XivCamp;
internal sealed class ProviderBridge(IDalamudPluginInterface pi):IWorkspaceProviders
{
    private string? Call(string owner,string method,object? parameters=null)
    {
        if(!CampScene.Owners.Contains(owner))return null;
        try{return pi.GetIpcSubscriber<string,string>("XivHud."+char.ToUpperInvariant(owner[0])+owner[1..]+".v1.Call").InvokeFunc(JsonSerializer.Serialize(new{method,@params=parameters??new{}},CampScene.Json));}catch{return null;}
    }
    public ProviderReading? Read(string owner)
    {
        try
        {
            using var caps=JsonDocument.Parse(Call(owner,"api.version")??"null");var root=caps.RootElement;
            if(!root.GetProperty("ok").GetBoolean())return null;
            var api=root.GetProperty("result");if(api.GetProperty("version").GetInt32()!=1)return null;
            var methods=api.GetProperty("methods").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
            using var response=JsonDocument.Parse(Call(owner,"status")??"null");var result=response.RootElement.GetProperty("result");
            if(result.TryGetProperty("observedAt",out var observed)&&(!observed.TryGetDateTimeOffset(out var time)||DateTimeOffset.UtcNow-time>TimeSpan.FromSeconds(3)))return null;
            var state=result.TryGetProperty("state",out var s)?s:result;
            if(state.TryGetProperty("ready",out var ready)&&ready.ValueKind==JsonValueKind.False)return null;
            var window=state.TryGetProperty("window",out var win)?win:state;
            var open=window.TryGetProperty("open",out var o)?o.GetBoolean():state.TryGetProperty("enabled",out var e)&&e.GetBoolean();
            var mode=window.TryGetProperty("requestedMode",out var m)?m.GetString()??"screen":"screen";
            if(mode=="screen"&&owner is "chat" or "combat" or "inventory" or "crafting"&&HostedElsewhere(owner))return null;
            var placement=window.TryGetProperty("placement",out var p)?p.GetString()??"Hud":"Hud";
            PanelRect? rect=null;
            if(window.TryGetProperty("layout",out var r)&&r.ValueKind==JsonValueKind.Object)rect=JsonSerializer.Deserialize<PanelRect>(r,CampScene.Json);
            if(rect is {Valid:false})rect=null;
            return new(new(owner,open,mode,placement,mode=="screen"?rect:null),methods);
        }
        catch{return null;}
    }
    private bool HostedElsewhere(string owner)
    {
        foreach(var channel in new[]{"XivHud.Windows.v1.Call","GhosttyDalamud.v1.Call"})
        {
            var gate=pi.GetIpcSubscriber<string,string>(channel);if(!gate.HasFunction)continue;
            try
            {
                var raw=gate.InvokeFunc("{\"method\":\"panel.list\"}");if(raw.Length>65536)return true;
                using var doc=JsonDocument.Parse(raw,new JsonDocumentOptions{MaxDepth=8});
                if(HostOwnership.ExternalScreenOwner(owner,doc.RootElement))return true;
            }
            catch{return true;} // An unavailable ownership report must not become a layout takeover.
        }
        return false;
    }
    public Ticket? Submit(WorkspaceOp operation)
    {
        if(operation.Method is not("window.open" or "window.close" or "window.mode" or "window.layout"))return null;
        try
        {
            using var response=JsonDocument.Parse(Call(operation.Owner,operation.Method,operation.Parameters)??"null");
            if(!response.RootElement.GetProperty("ok").GetBoolean())return null;
            var id=response.RootElement.GetProperty("result").GetProperty("requestId").GetString();return id is null?null:new(operation.Owner,id);
        }
        catch{return null;}
    }
    public string Poll(Ticket ticket)
    {
        try
        {
            using var response=JsonDocument.Parse(Call(ticket.Owner,"request.status",new{id=ticket.Id})??"null");
            if(!response.RootElement.GetProperty("ok").GetBoolean())return "request unavailable";
            var r=response.RootElement.GetProperty("result");return r.GetProperty("state").GetString()??"unknown";
        }
        catch{return "provider unavailable";}
    }
}
