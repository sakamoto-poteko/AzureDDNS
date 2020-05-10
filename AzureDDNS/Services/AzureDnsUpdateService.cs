using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Dns.Fluent;
using Microsoft.Azure.Management.Dns.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AzureDDNS.Services
{
    public class AzureDnsUpdateService : IDnsUpdateService
    {
        private readonly IDnsManagementClient dnsClient;
        private readonly Settings.DnsZone dnsZone;
        private readonly ILogger<AzureDnsUpdateService> logger;

        public AzureDnsUpdateService(IOptions<Settings.DnsZone> dnsZone, IOptions<Settings.AzureLogin> login, ILogger<AzureDnsUpdateService> logger)
        {
            this.dnsZone = dnsZone.Value;
            var settings = login.Value;

            AzureCredentials azureCredential;

            switch (settings.AzureCredentialType)
            {
                case Settings.AzureCredentialType.ManagedIdentity:
                    if (settings.AzureMsiType == Settings.AzureMsiType.Unknown)
                    {
                        throw new ArgumentOutOfRangeException(nameof(settings.AzureMsiType));
                    }
                    logger.LogInformation("Azure login with MSI");
                    azureCredential = GetMsiCredential(settings);
                    break;
                case Settings.AzureCredentialType.TokenCredential:
                    logger.LogInformation("Azure login with token credential");
                    azureCredential = GetTokenCredential(settings);
                    break;
                case Settings.AzureCredentialType.ServicePrincipal:
                    logger.LogInformation("Azure login with service principal");
                    azureCredential = GetServicePrincipalCredential(settings);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.AzureCredentialType));
            }

            var restClient = RestClient.Configure()
                .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                .WithCredentials(azureCredential)
                .Build();

            dnsClient = new DnsManagementClient(restClient)
            {
                SubscriptionId = settings.AzureSubscriptionId
            };

            this.logger = logger;
        }

        #region Azure Credentials

        private static AzureCredentials GetTokenCredential(Settings.AzureLogin settings)
        {
            if (settings.AzureTenantId == null)
            {
                throw new ArgumentNullException(nameof(settings.AzureTenantId));
            }

            var azureServiceTokenProvider = new AzureServiceTokenProvider();

            var token = azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com", settings.AzureTenantId).GetAwaiter().GetResult();
            var tokenCredentials = new Microsoft.Rest.TokenCredentials(token);
            return new AzureCredentials(tokenCredentials, tokenCredentials, settings.AzureTenantId, AzureEnvironment.AzureGlobalCloud);
        }

        private static AzureCredentials GetServicePrincipalCredential(Settings.AzureLogin settings)
        {
            if (settings.AzureClientId == null)
            {
                throw new ArgumentNullException(nameof(settings.AzureClientId));
            }

            if (settings.AzureKey == null)
            {
                throw new ArgumentNullException(nameof(settings.AzureKey));
            }

            if (settings.AzureTenantId == null)
            {
                throw new ArgumentNullException(nameof(settings.AzureTenantId));
            }

            var azureCredential = new AzureCredentialsFactory().FromServicePrincipal(settings.AzureClientId,
                settings.AzureKey, settings.AzureTenantId, AzureEnvironment.AzureGlobalCloud);
            return azureCredential;
        }

        private static AzureCredentials GetMsiCredential(Settings.AzureLogin settings)
        {
            var msiResourceType = settings.AzureMsiType switch
            {
                Settings.AzureMsiType.AppService => MSIResourceType.AppService,
                Settings.AzureMsiType.VirtualMachine => MSIResourceType.VirtualMachine,
                Settings.AzureMsiType.Unknown => throw new ArgumentOutOfRangeException(),
                _ => throw new ArgumentOutOfRangeException()
            };
            return new AzureCredentialsFactory().FromMSI(new MSILoginInformation(msiResourceType), AzureEnvironment.AzureGlobalCloud);
        }

        #endregion

        private string GetNoHostExceptionMessage(string recordName, string rgName)
        {
            return $"The resource record '{recordName.ToLower()}' does not exist in resource group '{rgName.ToLower()}' of subscription '{dnsClient.SubscriptionId.ToLower()}'.";
        }

        public async Task<IPAddress> GetDnsV4Async(string hostname)
        {
            try
            {
                var records = await GetDnsV4RecordsAsync(hostname);
                return IPAddress.Parse(records.ARecords.Single().Ipv4Address);
            }
            catch (RecordNotExistExcpetion)
            {
                return IPAddress.None;

            }
        }

        public async Task<IPAddress> GetDnsV6Async(string hostname)
        {
            try
            {
                var records = await GetDnsV6RecordsAsync(hostname);
                return IPAddress.Parse(records.AaaaRecords.Single().Ipv6Address);
            }
            catch (RecordNotExistExcpetion)
            {
                return IPAddress.None;
            }
        }

        public async Task<DnsUpdateResult> UpdateDnsV4Async(string hostname, IPAddress v4Address)
        {
            if (v4Address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentOutOfRangeException(nameof(v4Address));
            }

            RecordSetInner records;
            try
            {
                records = await GetDnsV4RecordsAsync(hostname);
            }
            catch (RecordNotExistExcpetion)
            {
                await CreateDnsV4RecordsAsync(hostname);
                records = await GetDnsV4RecordsAsync(hostname);
            }

            if (records.ARecords.Count == 0)
            {
                return DnsUpdateResult.Nohost;
            }

            if (IPAddress.Parse(records.ARecords.Single().Ipv4Address).Equals(v4Address))
            {
                return DnsUpdateResult.Nochg;
            }

            records.ARecords.Clear();
            records.ARecords.Add(new ARecord(v4Address.ToString()));

            await dnsClient.RecordSets.CreateOrUpdateAsync(dnsZone.ResourceGroup, dnsZone.ZoneName, hostname, RecordType.A, records);

            return DnsUpdateResult.Good;
        }

        private async Task<RecordSetInner> GetDnsV4RecordsAsync(string hostname)
        {
            try
            {
                var records = await dnsClient.RecordSets.GetAsync(dnsZone.ResourceGroup, dnsZone.ZoneName, hostname, RecordType.A);
                return records;
            }
            catch (CloudException ex)
            {
                if (ex.Message == GetNoHostExceptionMessage(hostname, dnsZone.ResourceGroup))
                {
                    throw new RecordNotExistExcpetion();
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task<RecordSetInner> GetDnsV6RecordsAsync(string hostname)
        {
            try
            {
                var records = await dnsClient.RecordSets.GetAsync(dnsZone.ResourceGroup, dnsZone.ZoneName, hostname, RecordType.AAAA);
                return records;
            }
            catch (CloudException ex)
            {
                if (ex.Message == GetNoHostExceptionMessage(hostname, dnsZone.ResourceGroup))
                {
                    throw new RecordNotExistExcpetion();
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task CreateDnsV4RecordsAsync(string hostname, List<IPAddress> addresses = default)
        {
            await dnsClient.RecordSets.CreateOrUpdateAsync(dnsZone.ResourceGroup, dnsZone.ZoneName, hostname, RecordType.A,
                new RecordSetInner(tTL: 0,
                aRecords: addresses == default ?
                new List<ARecord> { new ARecord(IPAddress.Any.ToString()) } :
                addresses.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).Select(addr => new ARecord(addr.ToString())).ToList()),
                ifNoneMatch: "*");
        }

        private async Task CreateDnsV6RecordsAsync(string hostname, List<IPAddress> addresses = default)
        {
            await dnsClient.RecordSets.CreateOrUpdateAsync(dnsZone.ResourceGroup, dnsZone.ZoneName, hostname, RecordType.AAAA,
                new RecordSetInner(tTL: 0,
                aaaaRecords: addresses == default ?
                new List<AaaaRecord> { new AaaaRecord(IPAddress.IPv6Any.ToString()) } :
                addresses.Where(addr => addr.AddressFamily == AddressFamily.InterNetworkV6).Select(addr => new AaaaRecord(addr.ToString())).ToList()),
                ifNoneMatch: "*");
        }

        public async Task<DnsUpdateResult> UpdateDnsV6Async(string hostname, IPAddress v6Address)
        {
            if (v6Address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ArgumentOutOfRangeException(nameof(v6Address));
            }

            RecordSetInner records;
            try
            {
                records = await GetDnsV6RecordsAsync(hostname);
            }
            catch (RecordNotExistExcpetion)
            {
                await CreateDnsV6RecordsAsync(hostname);
                records = await GetDnsV6RecordsAsync(hostname);
            }

            if (records.AaaaRecords.Count == 0)
            {
                return DnsUpdateResult.Nohost;
            }

            if (IPAddress.Parse(records.AaaaRecords.Single().Ipv6Address).Equals(v6Address))
            {
                return DnsUpdateResult.Nochg;
            }

            records.AaaaRecords.Clear();
            records.AaaaRecords.Add(new AaaaRecord(v6Address.ToString()));

            await dnsClient.RecordSets.CreateOrUpdateAsync(dnsZone.ResourceGroup, dnsZone.ZoneName, hostname, RecordType.AAAA, records);

            return DnsUpdateResult.Good;
        }
    }
}
