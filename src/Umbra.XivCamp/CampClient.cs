using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivCamp;

public enum CampLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, chat commands, or input hooks are used.</summary>
[Service]
public sealed class CampClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public CampClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivCamp.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivCamp.Ready");
        settings = pi.GetIpcSubscriber<object>("XivCamp.OpenSettings");
        Refresh();
    }

    public CampLinkState State { get; private set; } = CampLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        CampLinkState.Ready => "Open your Camp.",
        CampLinkState.Waiting => "Camp is getting ready. Try again in a moment.",
        _ => "Camp is unavailable. Enable XivCamp in the Dalamud plugin installer.",
    };

    [OnTick]
    private void Tick()
    {
        if (disposed || Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 1000;
        Refresh();
    }

    private void Refresh()
    {
        if (disposed) return;
        try
        {
            HasSettings = settings.HasAction;
            State = !toggle.HasAction || !ready.HasFunction ? CampLinkState.Missing
                : ready.InvokeFunc() ? CampLinkState.Ready : CampLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = CampLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != CampLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = CampLinkState.Missing;
            LogOnce(error); return false;
        }
    }

    public bool OpenSettings()
    {
        if (disposed) return false;
        Refresh();
        if (!HasSettings) return false;
        try { settings.InvokeAction(); return true; }
        catch (Exception error)
        {
            HasSettings = false;
            LogOnce(error); return false;
        }
    }

    private void LogOnce(Exception error)
    {
        var message = error.GetType().Name + ": " + error.Message;
        if (message == lastProblem) return;
        lastProblem = message;
        Logger.Warning("[Umbra.XivCamp] Camp connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = CampLinkState.Missing; HasSettings = false;
    }
}
