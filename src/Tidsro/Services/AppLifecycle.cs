namespace Tidsro.Services;

/// <summary>Where the app is in its life: starting, running, or exiting. Only a running app may open
/// its window or begin Quit. Startup and a failed final save both show dialogs that pump messages, so
/// the tray and a second launch's show signal can reach the app while it is not ready, or already on
/// its way out. Read from the show signal's thread-pool thread as well as the UI thread, so it locks.</summary>
public sealed class AppLifecycle
{
    private enum Stage { Starting, Running, Exiting }

    private readonly object _gate = new();
    private Stage _stage = Stage.Starting;

    /// <summary>Startup has finished and Quit has not begun.</summary>
    public bool IsRunning { get { lock (_gate) return _stage == Stage.Running; } }

    /// <summary>Quit has begun, or startup failed, or the app is shutting down.</summary>
    public bool IsExiting { get { lock (_gate) return _stage == Stage.Exiting; } }

    /// <summary>Startup has finished. Does nothing once exiting, so a failed startup stays failed.</summary>
    public void MarkStarted()
    {
        lock (_gate)
            if (_stage == Stage.Starting) _stage = Stage.Running;
    }

    /// <summary>True the first time Quit asks while the app is running; false on every later ask, and
    /// before startup has finished.</summary>
    public bool TryBeginQuit()
    {
        lock (_gate)
        {
            if (_stage != Stage.Running) return false;
            _stage = Stage.Exiting;
            return true;
        }
    }

    /// <summary>Startup failed or the app is shutting down. Nothing may open the window from now on.</summary>
    public void MarkExiting()
    {
        lock (_gate) _stage = Stage.Exiting;
    }
}
