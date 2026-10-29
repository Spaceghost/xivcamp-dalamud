using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using XivCamp.Core;
using XivHud.Style;
using static XivHud.Style.IllustratedStyle;
namespace XivCamp;

public sealed class Plugin:IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly ProviderBridge providers;
    private readonly CampSession session;
    private readonly IllustratedChrome chrome;
    private readonly ConcurrentQueue<(string Method,string Value)> pending=[];
    private readonly ICallGateProvider<string,string> call;
    private readonly ICallGateProvider<object> toggle,settings;
    private readonly ICallGateProvider<bool> ready;
    private string snapshot="{}",status="",name="My peaceful corner",selected="Campfire",lastRecommendation="";
    private bool open,disposed,registered,restoreWhenIdle;
    private long nextPublish,nextRecommendation;
    public Plugin(IDalamudPluginInterface pi,ICommandManager commands,IFramework framework,IClientState client,IPluginLog log)
    {
        this.pi=pi;this.commands=commands;this.framework=framework;this.client=client;this.log=log;
        config=pi.GetPluginConfig() as Configuration??new();
        config.Scenes=config.Scenes.Where(x=>x.Validate() is null).GroupBy(x=>x.Name,StringComparer.Ordinal).Select(x=>x.Last()).Take(16).ToList();if(config.Scenes.Count==0)config.Scenes=CampScene.Presets();
        config.Recovery=config.Recovery.Where(x=>new CampScene(1,"Recovery",[x]).Validate() is null).Take(16).ToList();
        providers=new(pi);session=new(providers,recovery=>{config.Recovery=recovery;Save();});chrome=new(pi);
        selected=config.Scenes[0].Name;
        call=pi.GetIpcProvider<string,string>("XivCamp.v1.Call");toggle=pi.GetIpcProvider<object>("XivCamp.Toggle");settings=pi.GetIpcProvider<object>("XivCamp.OpenSettings");ready=pi.GetIpcProvider<bool>("XivCamp.Ready");
        try
        {
            registered=commands.AddHandler("/xivcamp",new CommandInfo((_,arg)=>
            {
                var text=arg.Trim();if(text=="status"){log.Information("Camp status: {Status}",Volatile.Read(ref snapshot));return;}if(text is "restore" or "close" or "capture")Queue(text);else if(text.StartsWith("apply ",StringComparison.Ordinal))Queue("apply",text[6..]);else Queue("open");
            }){HelpMessage="Camp: open, apply NAME, capture, restore, close. Arrangements change only supported plugin UI."});
            if(!registered)throw new InvalidOperationException("/xivcamp is already registered");
            call.RegisterFunc(Call);toggle.RegisterAction(()=>Queue("toggle"));settings.RegisterAction(Open);ready.RegisterFunc(()=>!disposed);
            framework.Update+=Update;client.Logout+=Logout;pi.UiBuilder.Draw+=Draw;pi.UiBuilder.OpenMainUi+=Open;pi.UiBuilder.OpenConfigUi+=Open;
        }
        catch{Dispose();throw;}
    }
    private void Save()=>pi.SavePluginConfig(config);
    private void Open()=>Queue("open");
    private void Queue(string method,string value=""){if(!disposed&&pending.Count<32)pending.Enqueue((method,value));}
    private void Logout(int _,int __)=>Queue("restore");
    private string Call(string json)
    {
        if(disposed||json.Length>4096)return "{\"ok\":false,\"error\":\"unavailable\"}";
        try
        {
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=4});var method=doc.RootElement.GetProperty("method").GetString();
            if(method=="status")return Volatile.Read(ref snapshot);
            if(method=="api.version")return JsonSerializer.Serialize(new{ok=true,result=new{version=1,module="camp",methods=new[]{"status","window.open","window.close","scene.apply","scene.restore"}}});
            if(method is "window.open" or "window.close" or "scene.restore"){Queue(method=="window.open"?"open":method=="window.close"?"close":"restore");return "{\"ok\":true,\"result\":{\"queued\":true}}";}
            if(method=="scene.apply")
            {
                var value=doc.RootElement.GetProperty("params").GetProperty("name").GetString()??"";
                if(value.Length is <1 or >64||value.Any(char.IsControl))throw new ArgumentException();
                Queue("apply",value);return "{\"ok\":true,\"result\":{\"queued\":true}}";
            }
            return "{\"ok\":false,\"error\":\"unsupported_method\"}";
        }
        catch{return "{\"ok\":false,\"error\":\"invalid_request\"}";}
    }
    private void Update(IFramework _)
    {
        if(disposed)return;
        for(var n=0;n<8&&pending.TryDequeue(out var op);n++)
        {
            try
            {
                switch(op.Method)
                {
                    case "open":open=true;break;
                    case "toggle":if(open){open=false;if(!session.Busy)session.Restore();else restoreWhenIdle=true;}else open=true;break;
                    case "close":open=false;goto case "restore";
                    case "restore":if(!session.Busy)session.Restore(session.Recovery.Count==0?config.Recovery:null);else { restoreWhenIdle=true; status="Packing up when the current change finishes"; }break;
                    case "apply":
                        if(!client.IsLoggedIn){status="Log in before unfolding a workspace";break;}
                        var scene=config.Scenes.FirstOrDefault(x=>x.Name==op.Value);if(scene is null){status="Scene not found";break;}session.Apply(scene);status="";break;
                    case "capture":
                        if(session.Busy||session.ActiveName is not null){status="Pack up before capturing a new workspace";break;}
                        if(config.Scenes.Count>=16&&!config.Scenes.Any(x=>x.Name==op.Value)){status="Maximum 16 saved scenes";break;}
                        var panels=CampScene.Owners.Select(x=>providers.Read(x)?.State).Where(x=>x is not null).Select(x=>x!).ToList();
                        var capture=new CampScene(1,string.IsNullOrWhiteSpace(op.Value)?"My peaceful corner":op.Value,panels);
                        if(capture.Validate() is {} problem)throw new ArgumentException(problem);
                        config.Scenes.RemoveAll(x=>x.Name==capture.Name);config.Scenes.Add(capture);selected=capture.Name;Save();status="Saved your current workspace";break;
                    case "import":
                        var imported=CampScene.Import(op.Value);if(config.Scenes.Count>=16&&!config.Scenes.Any(x=>x.Name==imported.Name))throw new ArgumentException("Maximum 16 scenes");
                        config.Scenes.RemoveAll(x=>x.Name==imported.Name);config.Scenes.Add(imported);selected=imported.Name;Save();status="Imported arrangement · choose Unfold to apply";break;
                    case "delete":if(config.Scenes.Count>1){config.Scenes.RemoveAll(x=>x.Name==op.Value);selected=config.Scenes[0].Name;Save();}break;
                }
            }
            catch(Exception e){status=e is ArgumentException or InvalidOperationException?e.Message:"Workspace change unavailable";log.Warning("Camp UI request failed: {Type}",e.GetType().Name);}
        }
        try{session.Tick(Environment.TickCount64);}catch(Exception e){status="Workspace update interrupted · pack up to recover";log.Warning("Camp update failed: {Type}",e.GetType().Name);}
        if(restoreWhenIdle&&!session.Busy){restoreWhenIdle=false;session.Restore(session.Recovery.Count==0?config.Recovery:null);}
        var recommendation=(session.ActiveName??"")+"\n"+session.PrismProfile;
        if(recommendation!=lastRecommendation&&Environment.TickCount64>=nextRecommendation)
        {
            nextRecommendation=Environment.TickCount64+1000;
            try{var reply=pi.GetIpcSubscriber<string,string>("XivPrism.v1.Call").InvokeFunc(JsonSerializer.Serialize(new{method="profile.recommend",@params=new{scene=session.ActiveName??"",profile=session.PrismProfile}}));using var ack=JsonDocument.Parse(reply);if(ack.RootElement.GetProperty("ok").GetBoolean())lastRecommendation=recommendation;}catch{}
        }
        if(Environment.TickCount64<nextPublish)return;nextPublish=Environment.TickCount64+200;
        Volatile.Write(ref snapshot,JsonSerializer.Serialize(new{ok=true,result=new{version=1,module="camp",state=new{ready=true,activeScene=session.ActiveName,busy=session.Busy,phase=session.Busy?"changing":session.ActiveName is not null?"active":config.Recovery.Count>0?"recovery":"idle",status=session.Status,recoveryPanels=config.Recovery.Count,scenes=config.Scenes.Select(x=>x.Name),reports=session.Reports.Take(24),window=new{open}}}}));
    }
    private void Draw()
    {
        if(!open||disposed)return;
        var viewport=ImGui.GetMainViewport();var available=Vector2.Max(Vector2.One,viewport.WorkSize-new Vector2(24));
        ImGui.SetNextWindowSize(Vector2.Min(new(800,650),available),ImGuiCond.FirstUseEver);ImGui.SetNextWindowSizeConstraints(Vector2.Min(new(620,460),available),available);
        IllustratedChrome.BeginTheme();
        try
        {
            using var font=chrome.Body();var shown=open;
            var drew=ImGui.Begin("A place to settle###XivCamp",ref shown,ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.NoMove);
            try
            {
                if(!drew)return;
                var p=ImGui.GetWindowPos();var size=ImGui.GetWindowSize();var d=ImGui.GetWindowDrawList();Frame(d,p,size,1,true);chrome.Title(p+new Vector2(10,8),"Camp","Unfold your little corner of Eorzea.");
                ImGui.SetCursorScreenPos(p+new Vector2(20,10));ImGui.InvisibleButton("##binding",new(size.X-150,80));if(ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left))ImGui.SetWindowPos(p+ImGui.GetIO().MouseDelta);
                ImGui.SetCursorScreenPos(p+new Vector2(size.X-100,36));if(ImGui.Button("Close"))Queue("close");
                Rule(d,p+new Vector2(24,104),size.X-48,Gold,1);
                ImGui.SetCursorScreenPos(p+new Vector2(28,124));
                if(ImGui.BeginChild("##camp-content",size-new Vector2(56,160),false))
                {
                    ImGui.TextWrapped("Choose a quiet arrangement, or remember the workspace you have now. Packing up restores the windows you brought with you.");
                    ImGui.Spacing();if(ImGui.BeginCombo("Arrangement",selected))
                    {foreach(var s in config.Scenes)if(ImGui.Selectable(s.Name,s.Name==selected))selected=s.Name;ImGui.EndCombo();}
                    ImGui.BeginDisabled(session.Busy);if(ImGui.Button("Unfold this camp"))Queue("apply",selected);ImGui.SameLine();if(ImGui.Button("Pack up · restore workspace"))Queue("restore");ImGui.EndDisabled();
                    ImGui.TextColored(V(PaleGold),session.Status);if(status.Length>0)ImGui.TextWrapped(status);
                    if(config.Recovery.Count>0&&session.ActiveName is null&&!session.Busy){ImGui.TextWrapped("A saved recovery record is available.");if(ImGui.Button("Restore recorded workspace"))Queue("restore");}
                    var current=config.Scenes.FirstOrDefault(x=>x.Name==selected);
                    if(current is not null)
                    {
                        ImGui.Separator();foreach(var panel in current.Panels)ImGui.TextUnformatted(panel.Owner+"  ·  "+(panel.Open?"open":"closed")+"  ·  "+panel.Mode+(panel.Layout is null?"":"  ·  saved position"));
                        if(current.PrismProfile.Length>0)ImGui.TextDisabled("Prism layout suggestion: "+current.PrismProfile);
                    }
                    foreach(var report in session.Reports.Take(16))ImGui.TextWrapped(report);
                    ImGui.Separator();ImGui.InputText("New arrangement",ref name,64);
                    ImGui.BeginDisabled(session.Busy||session.ActiveName is not null);if(ImGui.Button("Remember current workspace"))Queue("capture",name);ImGui.EndDisabled();
                    if(current is not null){if(ImGui.Button("Copy arrangement"))ImGui.SetClipboardText(JsonSerializer.Serialize(current,CampScene.Json));ImGui.SameLine();if(ImGui.Button("Delete saved arrangement"))Queue("delete",selected);}
                    if(ImGui.Button("Import arrangement from clipboard")){var text=ImGui.GetClipboardText();if(text.Length<=65536)Queue("import",text);else status="Scene file exceeds 64 KiB";}
                    ImGui.TextWrapped("Missing modules are reported individually. Older modules keep their positions. Camp changes plugin windows only; it does not move your character, spawn furniture or start music.");
                }
                ImGui.EndChild();if(!shown)Queue("close");
            }
            finally{ImGui.End();}
        }
        finally{IllustratedChrome.EndTheme();}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        session.RestoreOnUnload();
        try{pi.GetIpcSubscriber<string,string>("XivPrism.v1.Call").InvokeFunc("{\"method\":\"profile.recommend\",\"params\":{\"scene\":\"\",\"profile\":\"\"}}");}catch{}
        framework.Update-=Update;client.Logout-=Logout;pi.UiBuilder.Draw-=Draw;pi.UiBuilder.OpenMainUi-=Open;pi.UiBuilder.OpenConfigUi-=Open;
        call.UnregisterFunc();toggle.UnregisterAction();settings.UnregisterAction();ready.UnregisterFunc();if(registered)commands.RemoveHandler("/xivcamp");chrome.Dispose();pending.Clear();
    }
}
