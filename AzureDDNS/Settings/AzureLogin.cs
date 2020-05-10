using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDDNS.Settings
{
    public class AzureLogin
    {
        [JsonConverter(typeof(StringEnumConverter))]
        [Required]
        public AzureCredentialType AzureCredentialType { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [Required]
        public AzureMsiType AzureMsiType { get; set; }

        [Required]
        public string AzureSubscriptionId { get; set; }

        public string AzureClientId { get; set; }

        public string AzureKey { get; set; }

        public string AzureTenantId { get; set; }
    }
}
