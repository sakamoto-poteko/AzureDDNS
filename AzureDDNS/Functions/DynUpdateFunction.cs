using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;

namespace AzureDDNS.Functions
{
    public class DynUpdateFunction
    {
        private readonly ILogger<DynUpdateFunction> logger;
        private readonly UpdateCore updateCore;

        public DynUpdateFunction(ILogger<DynUpdateFunction> logger, UpdateCore updateCore)
        {
            this.logger = logger;
            this.updateCore = updateCore;
        }

        [FunctionName("DynUpdateFunction")]
        public string DynUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v3")] HttpRequest req)
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

            var hosts = hostname.Split(",");

            var resultTasks = hosts.Select(h => updateCore.UpdateDnsRecord(myip, h)).ToArray();
            Task.WaitAll(resultTasks);

            var results = resultTasks.Select(r => r.Result).ToList();
            var result = string.Join("\n", results);

            return result;
        }
    }
}
