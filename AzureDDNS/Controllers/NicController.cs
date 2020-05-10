using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AzureDDNS.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AzureDDNS.Controllers
{
    [Route("nic")]
    [ApiController]
    public class NicController : ControllerBase
    {
        private readonly ILogger<NicController> logger;
        private readonly IDnsUpdateService dnsUpdateService;

        public NicController(ILogger<NicController> logger, IDnsUpdateService dnsUpdateService)
        {
            this.logger = logger;
            this.dnsUpdateService = dnsUpdateService;
        }

        [HttpGet]
        [Route("update")]
        public async Task<string> Update([FromQuery] string hostname, [FromQuery] string myip)
        {
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
    }
}