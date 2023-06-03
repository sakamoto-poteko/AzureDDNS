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
        private readonly IDnsUpdateService _dnsUpdateService;
        private readonly Settings.Authorization _auth;

        public UpdateCore(IDnsUpdateService dnsUpdateService, IOptions<Settings.Authorization> auth)
        {
            _dnsUpdateService = dnsUpdateService;
            _auth = auth.Value;
        }

        public bool CheckAuth(HttpRequest req)
        {
            if (!_auth.Enabled)
            {
                return true;
            }
            
            string authorizationString = req.Headers[HeaderNames.Authorization];

            if (string.IsNullOrWhiteSpace(authorizationString))
            {
                return false;
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationString, out var authenticationHeaderValue))
            {
                return false;
            }

            return authenticationHeaderValue.Scheme == "Basic" && authenticationHeaderValue.Parameter == _auth.GetBase64AuthorizationString();
        }

        public async Task<string> UpdateDnsRecord(string myip, string hostname)
        {
            string[] ips = myip.Split(',');
            var addresses = ips.Select(s =>
            {
                bool ok = IPAddress.TryParse(s, out var address);
                return ok ? address : null;
            }).Where(addr => addr != null).ToList();

            if (addresses.Count == 0)
            {
                var addr = await _dnsUpdateService.GetDnsV4Async(hostname);
                return addr.Equals( IPAddress.None) ? ReplyNohost : string.Format(ReplyNochg, addr.ToString());
            }

            var v4Addr = addresses.FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetwork);
            var v6Addr = addresses.FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetworkV6);

            DnsUpdateResult v4Result = DnsUpdateResult.Nochg, v6Result = DnsUpdateResult.Nochg;

            var returnAddr = new List<IPAddress>();

            if (v4Addr != null)
            {
                v4Result = await _dnsUpdateService.UpdateDnsV4Async(hostname, v4Addr);
                returnAddr.Add(v4Addr);
            }

            if (v6Addr != null)
            {
                v6Result = await _dnsUpdateService.UpdateDnsV6Async(hostname, v6Addr);
                returnAddr.Add(v6Addr);
            }

            if (v4Result == DnsUpdateResult.Nohost || v6Result == DnsUpdateResult.Nohost)
            {
                return ReplyNohost;
            }

            string returnAddrStr = string.Join(",", returnAddr);

            if (v4Result == DnsUpdateResult.Good || v6Result == DnsUpdateResult.Good)
            {
                return string.Format(ReplyGood, returnAddrStr);
            }

            if (v4Result == DnsUpdateResult.Nochg && v6Result == DnsUpdateResult.Nochg)
            {
                return string.Format(ReplyNochg, returnAddrStr);
            }

            // no conditions left
            throw new NotImplementedException("Unexpected condition");
        }

        private const string ReplyNochg = "nochg {0}";
        private const string ReplyGood = "good {0}";
        public const string ReplyNohost = "nohost";
        public const string ReplyBadauth = "badauth";
    }
}
