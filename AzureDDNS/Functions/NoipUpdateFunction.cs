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
        private readonly ILogger<NoipUpdateFunction> _logger;
        private readonly UpdateCore _updateCore;

        public NoipUpdateFunction(ILogger<NoipUpdateFunction> logger, UpdateCore updateCore)
        {
            this._logger = logger;
            this._updateCore = updateCore;
        }

        [FunctionName("NoipUpdateFunction")]
        public async Task<string> NoipUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nic/update")] HttpRequest req)
        {
            bool authOk = _updateCore.CheckAuth(req);
            if (!authOk)
            {
                return UpdateCore.ReplyBadauth;
            }

            string myip = req.Query["myip"];
            string hostname = req.Query["hostname"];

            myip ??= string.Empty;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return UpdateCore.ReplyNohost;
            }

            _logger.LogInformation($"Update requested with hostname '{hostname}' and IP '{myip}'");
            return await _updateCore.UpdateDnsRecord(myip, hostname);
        }

    }
}
