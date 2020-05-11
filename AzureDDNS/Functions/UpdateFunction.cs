using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using AzureDDNS.Services;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace AzureDDNS
{
    public class UpdateFunction
    {
        private readonly ILogger<UpdateFunction> logger;
        private readonly IDnsUpdateService dnsUpdateService;
        private readonly Settings.Authorization auth;

        public UpdateFunction(ILogger<UpdateFunction> logger, IDnsUpdateService dnsUpdateService, IOptions<Settings.Authorization> auth)
        {
            this.logger = logger;
            this.dnsUpdateService = dnsUpdateService;
            this.auth = auth.Value;
        }

        [FunctionName("Update")]
        public async Task<string> Update([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nic/update")] HttpRequest req)
        {
            if (auth.Enabled)
            {
                string authorizationString = req.Headers[HeaderNames.Authorization];

                if (string.IsNullOrWhiteSpace(authorizationString))
                {
                    return Badauth;
                }

                if (!AuthenticationHeaderValue.TryParse(authorizationString, out var authenticationHeaderValue))
                {
                    return Badauth;
                }

                if (authenticationHeaderValue.Scheme != "Basic" || authenticationHeaderValue.Parameter != auth.GetBase64AuthorizationString())
                {
                    return Badauth;
                }
            }

            string myip = req.Query["myip"];
            string hostname = req.Query["hostname"];

            myip ??= string.Empty;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return Nohost;
            }

            logger.LogInformation(string.Format("Update requested with hostname {0}", hostname));

            string[] ips = myip.Split(',');
            var addresses = ips.Select(s =>
            {
                var ok = IPAddress.TryParse(s, out var address);
                if (ok)
                {
                    return address;
                }
                else
                {
                    return null;
                }
            }).Where(addr => addr != null).ToList();

            if (addresses.Count == 0)
            {
                var addr = await dnsUpdateService.GetDnsV4Async(hostname);
                if (addr == IPAddress.None)
                {
                    return Nohost;
                }
                else
                {
                    return string.Format(Nochg, addr.ToString());
                }
            }

            var v4Addr = addresses.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
            var v6Addr = addresses.Where(addr => addr.AddressFamily == AddressFamily.InterNetworkV6).FirstOrDefault();

            DnsUpdateResult v4Result = DnsUpdateResult.Nochg, v6Result = DnsUpdateResult.Nochg;

            List<IPAddress> returnAddr = new List<IPAddress>();

            if (v4Addr != null)
            {
                v4Result = await dnsUpdateService.UpdateDnsV4Async(hostname, v4Addr);
                returnAddr.Add(v4Addr);
            }

            if (v6Addr != null)
            {
                v6Result = await dnsUpdateService.UpdateDnsV6Async(hostname, v6Addr);
                returnAddr.Add(v6Addr);
            }

            if (v4Result == DnsUpdateResult.Nohost || v6Result == DnsUpdateResult.Nohost)
            {
                return Nohost;
            }

            var returnAddrStr = string.Join(",", returnAddr);

            if (v4Result == DnsUpdateResult.Good || v6Result == DnsUpdateResult.Good)
            {
                return string.Format(Good, returnAddrStr);
            }

            if (v4Result == DnsUpdateResult.Nochg && v6Result == DnsUpdateResult.Nochg)
            {
                return string.Format(Nochg, returnAddrStr);
            }

            // no conditions left
            throw new NotImplementedException("Unexpected condition");
        }

        private const string Nochg = "nochg {0}";
        private const string Good = "good {0}";
        private const string Nohost = "nohost";
        private const string Badauth = "badauth";
    }
}
