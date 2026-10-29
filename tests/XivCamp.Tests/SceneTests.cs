using XivCamp.Core;
namespace XivCamp.Tests;
public sealed class SceneTests
{
    private sealed class Fake:IWorkspaceProviders
    {
        public Dictionary<string,ProviderReading> Readings=[];public List<WorkspaceOp> Submitted=[];public bool Refuse,Busy;
        public ProviderReading? Read(string owner)=>Readings.GetValueOrDefault(owner);
        public Ticket? Submit(WorkspaceOp op){Submitted.Add(op);return Refuse?null:new(op.Owner,Submitted.Count.ToString());}
        public string Poll(Ticket ticket)=>Busy?"queued":"applied";
    }
    private static ProviderReading Reading(string owner,bool layout=true)=>new(new(owner,false,Layout:new(.1f,.1f,.4f,.4f)),new HashSet<string>(layout?["window.open","window.close","window.mode","window.layout"]:["window.open","window.close"]));
    private static CampScene Scene()=>new(1,"Campfire",[new("chat",true,Layout:new(.5f,.2f,.4f,.5f))],"Desk");
    [Fact]public void PresetsAndRoundTripsUseOnlyVersionedUiStates()
    {
        foreach(var scene in CampScene.Presets()){Assert.Null(scene.Validate());var restored=CampScene.Import(System.Text.Json.JsonSerializer.Serialize(scene,CampScene.Json));Assert.Equal(scene.Name,restored.Name);Assert.Equal(scene.Panels.Count,restored.Panels.Count);}
    }
    [Theory][InlineData("{\"version\":2}")][InlineData("{\"version\":1,\"name\":\"X\",\"panels\":null}")][InlineData("{\"version\":1,\"name\":\"X\",\"panels\":[{\"owner\":\"execute_command\",\"open\":true}]}")]
    public void InvalidOrFutureScenesAreRejectedBeforeAnOperation(string json)=>Assert.ThrowsAny<Exception>(()=>CampScene.Import(json));
    [Fact]public void OffscreenAndNonFiniteGeometryIsRejected()
    {
        Assert.NotNull((Scene() with{Panels=[new("chat",true,Layout:new(float.NaN,0,.5f,.5f))]}).Validate());
        Assert.NotNull((Scene() with{Panels=[new("chat",true,Layout:new(.8f,0,.5f,.5f))]}).Validate());
        Assert.NotNull((Scene() with{Panels=[new("chat",true),new("chat",false)]}).Validate());
        Assert.Throws<ArgumentException>(()=>CampScene.Import(new string('x',65537)));
    }
    [Fact]public void RecoveryIsPersistedBeforeAnyUiChangeAndRestoreIsAcknowledged()
    {
        var fake=new Fake();fake.Readings["chat"]=Reading("chat");var saved=new List<List<PanelState>>();var session=new CampSession(fake,x=>saved.Add(x));session.Apply(Scene());
        Assert.Single(saved);Assert.False(saved[0][0].Open);Assert.Empty(fake.Submitted);
        for(var i=0;i<10;i++)session.Tick(i*100);Assert.Equal("Campfire",session.ActiveName);Assert.Contains(fake.Submitted,x=>x.Method=="window.layout");
        session.Restore();for(var i=10;i<20;i++)session.Tick(i*100);Assert.Null(session.ActiveName);Assert.Empty(saved[^1]);Assert.Contains(fake.Submitted,x=>x.Method=="window.close");
    }
    [Fact]public void OldProvidersKeepTheirPositionsAndMissingProvidersAreReported()
    {
        var fake=new Fake();fake.Readings["chat"]=Reading("chat",false);var session=new CampSession(fake,_=>{});
        session.Apply(Scene() with{Panels=[new("journal",true),..Scene().Panels]});for(var i=0;i<10;i++)session.Tick(i*100);
        Assert.DoesNotContain(fake.Submitted,x=>x.Method=="window.layout");Assert.Contains(session.Reports,x=>x.Contains("position kept"));Assert.Contains(session.Reports,x=>x.Contains("journal"));
    }
    [Fact]public void FailedRequestsRollbackAndKeepRecoveryIfRestorationIsAlsoRefused()
    {
        var fake=new Fake{Refuse=true};fake.Readings["chat"]=Reading("chat");var session=new CampSession(fake,_=>{});session.Apply(Scene());
        for(var i=0;i<10;i++)session.Tick(i*100);Assert.Null(session.ActiveName);Assert.NotEmpty(session.Recovery);Assert.Contains("recovery",session.Status);
    }
    [Fact]public void PendingRequestsTimeOutAndSceneSwitchesCannotOverlap()
    {
        var fake=new Fake{Busy=true};fake.Readings["chat"]=Reading("chat");var session=new CampSession(fake,_=>{});session.Apply(Scene());session.Tick(0);
        Assert.Throws<InvalidOperationException>(()=>session.Apply(Scene()));session.Tick(5000);Assert.Null(session.ActiveName);
    }
    [Fact]public void UnloadQueuesOnlyRestoreOperationsAndRetainsTheRecoveryJournal()
    {
        var fake=new Fake();fake.Readings["chat"]=Reading("chat");var session=new CampSession(fake,_=>{});session.Apply(Scene());for(var i=0;i<10;i++)session.Tick(i*100);
        var previous=fake.Submitted.Count;session.RestoreOnUnload();Assert.True(fake.Submitted.Count>previous);Assert.NotEmpty(session.Recovery);Assert.All(fake.Submitted,x=>Assert.StartsWith("window.",x.Method));
    }
    [Fact]public void AnUnobservedWindowIsNeverMovedWithoutARecoverableOriginalRectangle()
    {
        var reading=Reading("chat") with{State=new("chat",false)};var reports=new List<string>();
        var plan=CampSession.Plan(reading,Scene().Panels[0],reports);
        Assert.DoesNotContain(plan,x=>x.Method=="window.layout");Assert.Contains(reports,x=>x.Contains("opened once"));
        Assert.Contains(plan,x=>x.Method=="window.open");
    }

}
