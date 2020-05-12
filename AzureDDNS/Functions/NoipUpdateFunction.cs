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
using AzureDDNS.Functions;

namespace AzureDDNS
{
    public class NoipUpdateFunction
    {
        private readonly ILogger<NoipUpdateFunction> logger;
        private readonly UpdateCore updateCore;

        public NoipUpdateFunction(ILogger<NoipUpdateFunction> logger, UpdateCore updateCore)
        {
            this.logger = logger;
            this.updateCore = updateCore;
        }

        [FunctionName("NoipUpdateFunction")]
        public async Task<string> NoipUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nic/update")] HttpRequest req)
        {
            var authOk = updateCore.CheckAuth(req);
            if (!authOk)
            {
                return UpdateCore.Badauth;
            }

            string myip = req.Query["myip"];
            string hostname = req.Query["hostname"];

            myip ??= string.Empty;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return UpdateCore.Nohost;
            }

            logger.LogInformation(string.Format("Update requested with hostname '{0}' and IP '{1}'", hostname, myip));
            return await updateCore.UpdateDnsRecord(myip, hostname);
        }

    }
}
