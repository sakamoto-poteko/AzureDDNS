using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace AzureDDNS.Services
{
    public interface IDnsUpdateService
    {
        Task<DnsUpdateResult> UpdateDnsV4Async(string hostname, IPAddress v4Address);
        Task<DnsUpdateResult> UpdateDnsV6Async(string hostname, IPAddress v6Address);
        Task<IPAddress> GetDnsV4Async(string hostname);
        Task<IPAddress> GetDnsV6Async(string hostname);
    }
}
