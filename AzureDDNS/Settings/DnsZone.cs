using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDDNS.Settings
{
    public class DnsZone
    {
        public string ResourceGroup { get; set; }

        public string ZoneName { get; set; }
    }
}
