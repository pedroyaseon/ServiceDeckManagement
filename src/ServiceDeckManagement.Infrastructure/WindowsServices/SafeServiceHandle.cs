using Microsoft.Win32.SafeHandles;

namespace ServiceDeckManagement.Infrastructure.WindowsServices;

internal sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeServiceHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeServiceMethods.CloseServiceHandle(handle);
}
