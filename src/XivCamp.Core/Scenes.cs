using System.Text.Json;
namespace XivCamp.Core;
public sealed record PanelRect(float X,float Y,float Width,float Height)
{
    public bool Valid=>float.IsFinite(X)&&float.IsFinite(Y)&&float.IsFinite(Width)&&float.IsFinite(Height)&&X>=0&&Y>=0&&Width is >=.1f and <=1&&Height is >=.1f and <=1&&X+Width<=1.001f&&Y+Height<=1.001f;
}
public sealed record PanelState(string Owner,bool Open,string Mode="screen",string Placement="Hud",PanelRect? Layout=null);
public sealed record CampScene(int Version,string Name,List<PanelState> Panels,string PrismProfile="")
{
    public static readonly string[] Owners=["journal","character","vitals","combat","chat","inventory","crafting","windows"];
    public string? Validate()
    {
        if(Version!=1)return "Unsupported scene version";
        if(string.IsNullOrWhiteSpace(Name)||Name.Length>64||Name.Any(char.IsControl))return "Choose a scene name up to 64 characters";
        if(PrismProfile is null||PrismProfile.Length>64||PrismProfile.Any(char.IsControl))return "Invalid Prism profile";
        if(Panels is null||Panels.Any(x=>x is null)||Panels.Count is <1 or >16||Panels.Select(x=>x.Owner).Distinct().Count()!=Panels.Count)return "Invalid or duplicate panels";
        foreach(var p in Panels)
            if(!Owners.Contains(p.Owner)||p.Mode is not("screen" or "world" or "native_frame")||p.Placement is not("Hud" or "Companion" or "World" or "Ground")||p.Layout is {Valid:false}||p.Layout is not null&&p.Mode!="screen")return "Invalid panel state";
        return null;
    }
    public static CampScene Import(string json)
    {
        if(json.Length>65536)throw new ArgumentException("Scene file exceeds 64 KiB");
        var value=JsonSerializer.Deserialize<CampScene>(json,Json)??throw new ArgumentException("Invalid scene file");
        if(value.Validate() is {} error)throw new ArgumentException(error);return value;
    }
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,MaxDepth=8};
    public static List<CampScene> Presets()=>[
        new(1,"Campfire",[new("journal",true,Layout:new(.02f,.10f,.52f,.78f)),new("chat",true,Layout:new(.56f,.10f,.42f,.65f)),new("combat",false),new("vitals",false)],"Desk"),
        new(1,"Work corner",[new("journal",false),new("chat",true,Layout:new(.53f,.10f,.45f,.72f)),new("inventory",true),new("crafting",true)],"Desk"),
        new(1,"Adventure",[new("journal",false),new("chat",false),new("combat",true),new("vitals",true)],"Peripheral")];
}
public sealed record ProviderReading(PanelState State,IReadOnlySet<string> Methods);
public sealed record WorkspaceOp(string Owner,string Method,object Parameters);
public sealed record Ticket(string Owner,string Id);
public interface IWorkspaceProviders
{
    ProviderReading? Read(string owner);
    Ticket? Submit(WorkspaceOp operation);
    string Poll(Ticket ticket);
}

/// <summary>Bounded, incremental UI-only transaction with acknowledged requests and rollback.</summary>
public sealed class CampSession(IWorkspaceProviders providers,Action<List<PanelState>> persistRecovery)
{
    private readonly Queue<WorkspaceOp> operations=[];
    private List<PanelState> originals=[];
    private Ticket? pending;
    private long sentAt,nextHealth;
    private bool restoring, restoreFailed;
    private CampScene? switching;
    public string? ActiveName{get;private set;}
    public string Status{get;private set;}="Choose a place to settle";
    public List<string> Reports{get;}=[];
    public bool Busy=>pending is not null||operations.Count>0;
    public IReadOnlyList<PanelState> Recovery=>originals;
    public string PrismProfile{get;private set;}="";
    public void Apply(CampScene scene)
    {
        if(scene.Validate() is {} error)throw new ArgumentException(error);
        if(Busy)throw new InvalidOperationException("A workspace change is still in progress");
        if(ActiveName is not null){switching=scene;Restore();return;}
        Reports.Clear();originals=[];var planned=new List<WorkspaceOp>();
        foreach(var target in scene.Panels)
        {
            var current=providers.Read(target.Owner);
            if(current is null){Reports.Add(target.Owner+": not installed, unavailable or hosted elsewhere");continue;}
            originals.Add(current.State);planned.AddRange(Plan(current,target,Reports));
        }
        if(originals.Count==0){Status="No available scene panels";return;}
        persistRecovery(originals.ToList());foreach(var op in planned)operations.Enqueue(op);
        ActiveName=scene.Name;PrismProfile=scene.PrismProfile;restoring=false;Status="Unfolding "+scene.Name;
        if(!Busy)Status="Settled in "+scene.Name;
    }
    public static List<WorkspaceOp> Plan(ProviderReading from,PanelState to,List<string> reports)
    {
        var result=new List<WorkspaceOp>();var owner=to.Owner;
        if(from.State.Mode!=to.Mode||to.Mode=="world"&&from.State.Placement!=to.Placement)
        {
            if(!from.Methods.Contains("window.mode")){reports.Add(owner+": current host kept; provider has no mode control");return result;}
            result.Add(new(owner,"window.mode",to.Mode=="world"?new{mode=to.Mode,placement=to.Placement}:(object)new{mode=to.Mode}));
        }
        if(to.Layout is {} rect)
        {
            if(from.Methods.Contains("window.layout")&&from.State.Layout is not null)result.Add(new(owner,"window.layout",new{x=rect.X,y=rect.Y,width=rect.Width,height=rect.Height}));
            else reports.Add(owner+(from.Methods.Contains("window.layout")?": position kept until this window has been opened once":": position kept; update the module for saved layout support"));
        }
        var method=to.Open?"window.open":"window.close";
        if(from.Methods.Contains(method))result.Add(new(owner,method,new{}));
        else reports.Add(owner+": visibility control unavailable");
        return result;
    }
    public void Restore(List<PanelState>? recovery=null)
    {
        if(Busy)throw new InvalidOperationException("A workspace change is still in progress");
        if(recovery is not null)originals=recovery.ToList();
        restoreFailed=false;restoring=true;ActiveName=null;PrismProfile="";Status="Packing up · restoring your workspace";
        BuildRestore();if(!Busy)FinishRestore();
    }
    private void BuildRestore()
    {
        foreach(var state in originals.AsEnumerable().Reverse())
        {
            var current=providers.Read(state.Owner);
            if(current is null){Reports.Add(state.Owner+": restore waits for the provider");restoreFailed=true;continue;}
            if(state.Layout is not null&&!current.Methods.Contains("window.layout"))restoreFailed=true;
            if(current.State.Mode!=state.Mode&&!current.Methods.Contains("window.mode"))restoreFailed=true;
            foreach(var op in Plan(current,state,Reports))operations.Enqueue(op);
        }
    }
    public void Tick(long now)
    {
        if(pending is {} ticket)
        {
            var state=providers.Poll(ticket);
            if(state=="queued"&&now>=sentAt&&now-sentAt<4000)return;
            pending=null;
            if(state!="applied")
            {
                Reports.Add(ticket.Owner+": "+(state=="queued"?"request timed out":state));
                if(!restoring){Fail();return;}
                restoreFailed=true;
            }
        }
        if(operations.TryDequeue(out var operation))
        {
            pending=providers.Submit(operation);sentAt=now;
            if(pending is null){Reports.Add(operation.Owner+": UI request refused");if(!restoring)Fail();else restoreFailed=true;}
            return;
        }
        if(restoring){FinishRestore();return;}
        if(ActiveName is null)return;
        Status="Settled in "+ActiveName;
        if(now<nextHealth)return;nextHealth=now+1000;
        if(originals.Any(x=>providers.Read(x.Owner) is null)){Reports.Add("A scene provider disconnected; restoring remaining panels");Fail();}
    }
    private void Fail()
    {
        operations.Clear();pending=null;restoreFailed=false;restoring=true;ActiveName=null;PrismProfile="";Status="Restoring after an unavailable panel";BuildRestore();
    }
    private void FinishRestore()
    {
        if(originals.Any(x=>providers.Read(x.Owner) is null)||restoreFailed)
        {Status="Some panels need recovery · original state retained";switching=null;restoring=false;return;}
        originals.Clear();persistRecovery([]);restoring=false;Status="Packed up · your previous workspace is back";
        if(switching is {} next){switching=null;Apply(next);}
    }
    public void RestoreOnUnload()
    {
        foreach(var state in originals.AsEnumerable().Reverse())
            if(providers.Read(state.Owner) is {} current)
                foreach(var op in Plan(current,state,Reports))providers.Submit(op);
        // Preserve the recovery journal until acknowledged restoration, including reload/crash recovery.
    }
}
