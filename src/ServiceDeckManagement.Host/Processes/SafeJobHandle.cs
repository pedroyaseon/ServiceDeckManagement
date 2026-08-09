using Microsoft.Win32.SafeHandles;

namespace ServiceDeckManagement.Host.Processes;

internal sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeJobHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() => WindowsJobNative.CloseHandle(handle);
}
