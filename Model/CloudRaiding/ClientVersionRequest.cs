using System;
using System.Reflection;

namespace SWTORCombatParser.Model.CloudRaiding;

internal static class ClientVersionRequest
{
    // Reuse the existing launch-time request. No machine, account or character identifier is sent.
    internal static Uri CreateUri(string apiPath) => BuildUri(apiPath,
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "unknown");

    internal static Uri BuildUri(string apiPath, string version, string platform) =>
        new($"{apiPath.TrimEnd('/')}/SoftwareVersion?client=desktop&version={Uri.EscapeDataString(version)}&platform={Uri.EscapeDataString(platform)}");
}
