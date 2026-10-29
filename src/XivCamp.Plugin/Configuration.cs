using Dalamud.Configuration;
using XivCamp.Core;
namespace XivCamp;
[Serializable]
public sealed class Configuration:IPluginConfiguration
{
    public int Version{get;set;}=1;
    public List<CampScene> Scenes{get;set;}= [];
    public List<PanelState> Recovery{get;set;}=[];
}
