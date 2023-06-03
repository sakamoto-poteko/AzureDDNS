using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace AzureDDNS.Functions
{
    public class DynUpdateFunction
    {
        private readonly ILogger<DynUpdateFunction> _logger;
        private readonly UpdateCore _updateCore;

        public DynUpdateFunction(ILogger<DynUpdateFunction> logger, UpdateCore updateCore)
        {
            _logger = logger;
            _updateCore = updateCore;
        }

        [FunctionName("DynUpdateFunction")]
        public string DynUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v4")] HttpRequest req)
        {
            bool authOk = _updateCore.CheckAuth(req);
            if (!authOk)
            {
                return UpdateCore.ReplyBadauth;
            }

            string myIp = req.Query["myip"];
            string hostname = req.Query["hostname"];

            myIp ??= string.Empty;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return UpdateCore.ReplyNohost;
            }

            _logger.LogInformation("Update requested with hostname '{Hostname}' and IP '{MyIp}'", hostname, myIp);

            string[] hosts = hostname.Split(",");

            var resultTasks = hosts.Select(h => _updateCore.UpdateDnsRecord(myIp, h)).ToArray();
            Task.WaitAll(resultTasks);

            var results = resultTasks.Select(r => r.Result).ToList();
            string result = string.Join("\n", results);

            return result;
        }
    }
}