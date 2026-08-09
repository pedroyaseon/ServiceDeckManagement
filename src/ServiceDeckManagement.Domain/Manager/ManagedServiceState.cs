namespace ServiceDeckManagement.Domain.Manager;

public enum ManagedServiceState
{
    Missing,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused,
    Unknown
}
