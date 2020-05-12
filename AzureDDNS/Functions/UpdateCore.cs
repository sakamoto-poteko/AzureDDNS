using AzureDDNS.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AzureDDNS.Functions
{
    public class UpdateCore
    {
        private readonly IDnsUpdateService dnsUpdateService;
        private readonly Settings.Authorization auth;

        public UpdateCore(IDnsUpdateService dnsUpdateService, IOptions<Settings.Authorization> auth)
        {
            this.dnsUpdateService = dnsUpdateService;
            this.auth = auth.Value;
        }

        public bool CheckAuth(HttpRequest req)
        {
            if (auth.Enabled)
            {
                string authorizationString = req.Headers[HeaderNames.Authorization];

                if (string.IsNullOrWhiteSpace(authorizationString))
                {
                    return false;
                }

                if (!AuthenticationHeaderValue.TryParse(authorizationString, out var authenticationHeaderValue))
                {
                    return false;
                }

                if (authenticationHeaderValue.Scheme != "Basic" || authenticationHeaderValue.Parameter != auth.GetBase64AuthorizationString())
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<string> UpdateDnsRecord(string myip, string hostname)
        {
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

        public const string Nochg = "nochg {0}";
        public const string Good = "good {0}";
        public const string Nohost = "nohost";
        public const string Badauth = "badauth";
    }
}
