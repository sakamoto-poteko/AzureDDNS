using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDDNS.Settings
{
    public class DnsZone
    {
        [Required]
        public string ResourceGroup { get; set; }

        [Required]
        public string ZoneName { get; set; }

        [Required]
        public string ZoneResourceId { get; set; }
    }
}
