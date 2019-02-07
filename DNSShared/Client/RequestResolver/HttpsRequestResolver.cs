using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DNS.Protocol;
using DNS.Protocol.Utils;

namespace DNS.Client.RequestResolver
{
    // RFC 8484
    public class HttpsRequestResolver : IRequestResolver
    {
        public string Uri { get; set; }
        public int Timeout { get; set; } = 10000;

        private HttpClient httpClient = new HttpClient();

        public async Task<IResponse> Resolve(IRequest request)
        {
            // TODO: HTTP/2
            // TODO: ensure Uri is https?
            request.Id = 0;
            var reqUri = Uri + "?dns=" + Convert.ToBase64String(request.ToArray());
            var req = new HttpRequestMessage(HttpMethod.Get, reqUri);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/dns-message"));

            var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, new CancellationTokenSource(Timeout).Token);

            if (resp.Content.Headers.ContentType.MediaType != "application/dns-message") {
                throw new Exception("wrong response type");
            }

            var buffer = await resp.Content.ReadAsByteArrayAsync();

            Response response = Response.FromArray(buffer);

            if (response.Truncated) {
                throw new Exception("response truncated in DoH?!");
            }

            return new ClientResponse(request, response, buffer);
        }
    }
}
