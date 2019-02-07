using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using DNS.Protocol;
using DNS.Protocol.Utils;

namespace DNS.Client.RequestResolver {
    public class UdpRequestResolver : IRequestResolver {
        private int retries = 3;
        private int timeout;
        private IRequestResolver fallback;
        private IPEndPoint dns;

        public UdpRequestResolver(IPEndPoint dns, IRequestResolver fallback, int timeout = 5000) {
            this.dns = dns;
            this.fallback = fallback;
            this.timeout = timeout;
        }

        public UdpRequestResolver(IPEndPoint dns, int timeout = 5000) {
            this.dns = dns;
            this.fallback = new NullRequestResolver();
            this.timeout = timeout;
        }

        public async Task<IResponse> Resolve(IRequest request) {
            int retry = retries;
            IPEndPoint localEP = null;
            RETRY:
            using (UdpClient udp = (localEP == null ? new UdpClient() : new UdpClient(localEP))) {
                await udp
                    .SendAsync(request.ToArray(), request.Size, dns)
                    .WithCancellationTimeout(timeout);

                localEP = (IPEndPoint)udp.Client.LocalEndPoint;

                UdpReceiveResult result;
                try {
                    result = await udp.ReceiveAsync().WithCancellationTimeout(timeout);
                    if (!result.RemoteEndPoint.Equals(dns)) throw new IOException("Remote endpoint mismatch");
                } catch (System.Exception) when (--retry > 0) {
                    goto RETRY;
                }
                byte[] buffer = result.Buffer;
                Response response = Response.FromArray(buffer);

                if (response.Truncated) {
                    return await fallback.Resolve(request);
                }

                return new ClientResponse(request, response, buffer);
            }
        }
    }
}
