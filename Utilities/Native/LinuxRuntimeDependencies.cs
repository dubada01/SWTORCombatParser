using System;
using System.Runtime.InteropServices;

namespace SWTORCombatParser.Utilities.Native;

internal static class LinuxRuntimeDependencies
{
    internal static string? GetStartupError()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try
        {
            // Query the runtime's own loader, including its version override and
            // search paths. Unlike EnsureOpenSslInitialized, this returns 0 when
            // no supported libssl can be loaded instead of aborting the process.
            return OpenSslAvailable() == 1 ? null : MissingOpenSslMessage;
        }
        catch (DllNotFoundException)
        {
            return "Orbs could not load its .NET cryptography library. Extract the complete " +
                   "Linux package again, keeping all files together.\n\n" + MissingOpenSslMessage;
        }
        catch (EntryPointNotFoundException)
        {
            return "Orbs could not check OpenSSL with this .NET runtime. Use the complete " +
                   "self-contained Linux build, or a supported .NET 8 runtime.";
        }
    }

    internal const string MissingOpenSslMessage =
        "Online features need a compatible OpenSSL library. You can continue offline " +
        "to use local logs, overlays and timers. The bundled .NET 8 " +
        "runtime uses OpenSSL 3; OpenSSL 4 alone does not provide libssl.so.3.\n\n" +
        "On Fedora 45, install the compatibility package in Terminal:\n" +
        "sudo dnf install openssl3-libs\n\n" +
        "On other distributions, install the package providing libssl.so.3 and libcrypto.so.3. " +
        "Then start Orbs again.";

    [DllImport("System.Security.Cryptography.Native.OpenSsl",
        EntryPoint = "CryptoNative_OpenSslAvailable", CallingConvention = CallingConvention.Cdecl)]
    private static extern int OpenSslAvailable();
}
