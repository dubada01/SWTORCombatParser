using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities;

internal sealed class OfflineHttpHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    internal static HttpClient CreateClient(HttpMessageHandler? inner = null) =>
        new(new OfflineHttpHandler(inner ?? new HttpClientHandler()));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Last gate before DNS, sockets or TLS, including requests from UI paths
        // that did not check offline mode themselves.
        if (Settings.ReadSettingOfType<bool>("offline_mode"))
            return Task.FromException<HttpResponseMessage>(new InvalidOperationException("This feature is unavailable in offline mode."));
        return base.SendAsync(request, cancellationToken);
    }
}
