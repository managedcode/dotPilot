#if DEBUG
using System.Diagnostics;
using System.Globalization;
using System.Text;
#endif

namespace DotPilot.Core.HttpDiagnostics;

public sealed class DebugHttpHandler(HttpMessageHandler? innerHandler = null)
    : DelegatingHandler(innerHandler ?? new HttpClientHandler())
{
#if DEBUG
    private const string UnsuccessfulApiCallMessage = "Unsuccessful API call";
    private const string RequestUriFormat = "{0} ({1})";
    private const string HeaderFormat = "{0}: {1}";
    private const string HeaderSeparator = ", ";
    private static readonly CompositeFormat RequestUriCompositeFormat = CompositeFormat.Parse(RequestUriFormat);
    private static readonly CompositeFormat HeaderCompositeFormat = CompositeFormat.Parse(HeaderFormat);
#endif

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
#if DEBUG
        if (!response.IsSuccessStatusCode)
        {
            Trace.WriteLine(UnsuccessfulApiCallMessage);

            if (request.RequestUri is not null)
            {
                Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, RequestUriCompositeFormat, request.RequestUri, request.Method));
            }

            foreach (var header in request.Headers)
            {
                Trace.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    HeaderCompositeFormat,
                    header.Key,
                    string.Join(HeaderSeparator, header.Value)));
            }

            if (request.Content is null)
            {
                return response;
            }

            var content = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(content))
            {
                Trace.WriteLine(content);
            }
        }
#endif

        return response;
    }
}
