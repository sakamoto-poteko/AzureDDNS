using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureDDNS.Services;

public class AzureRmDnsUpdateService : IDnsUpdateService
{
    private readonly ILogger<AzureRmDnsUpdateService> _logger;
    private readonly DnsZoneResource _zoneResource;

    public AzureRmDnsUpdateService(IOptions<Settings.DnsZone> dnsZone, IOptions<Settings.AzureLogin> login,
        ILogger<AzureRmDnsUpdateService> logger)
    {
        var settings = login.Value;
        _logger = logger;

        TokenCredential azureCredential;
        switch (settings.AzureCredentialType)
        {
            case Settings.AzureCredentialType.TokenCredential:
//              case Settings.AzureCredentialType.DefaultCredential: - the same
                logger.LogInformation("Azure login with token credential");
                azureCredential = new DefaultAzureCredential();
                break;
            case Settings.AzureCredentialType.ServicePrincipal:
                logger.LogInformation("Azure login with service principal");
                azureCredential = new ClientSecretCredential(settings.AzureTenantId, settings.AzureClientId,
                    settings.AzureKey);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings.AzureCredentialType));
        }

        var armClient = new ArmClient(azureCredential, settings.AzureSubscriptionId, new ArmClientOptions
        {
            // customizable
            Environment = ArmEnvironment.AzurePublicCloud
        });
        _zoneResource = armClient.GetDnsZoneResource(new ResourceIdentifier(dnsZone.Value.ZoneResourceId));
    }

    public async Task<IPAddress> GetDnsV4Async(string hostname)
    {
        var records = await _zoneResource.GetDnsARecordAsync(hostname);
        if (!records.HasValue)
        {
            return IPAddress.None;
        }

        var aRecords = records.Value.Data.DnsARecords.Select(rec => rec.IPv4Address).ToList();
        return aRecords.Single();
    }

    public async Task<IPAddress> GetDnsV6Async(string hostname)
    {
        var records = await _zoneResource.GetDnsAaaaRecordAsync(hostname);
        if (!records.HasValue)
        {
            return IPAddress.None;
        }

        var aaaaRecords = records.Value.Data.DnsAaaaRecords.Select(rec => rec.IPv6Address).ToList();
        return aaaaRecords.Single();
    }

    public async Task<DnsUpdateResult> UpdateDnsV4Async(string hostname, IPAddress v4Address)
    {
        if (v4Address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentOutOfRangeException(nameof(v4Address));
        }

        var response = await _zoneResource.GetDnsARecordAsync(hostname);
        // is it an existing record?
        if (response.HasValue)
        {
            DnsARecordResource recordResource = response.Value;
            // the value is the same
            if (recordResource.Data.DnsARecords.Select(record => record.IPv4Address).Single().Equals(v4Address))
            {
                return DnsUpdateResult.Nochg;
            }
            // or the value is different?
            else
            {
                var resourceId = recordResource.Id;
                var data = new DnsARecordData()
                {
                    DnsARecords = { new DnsARecordInfo() { IPv4Address = v4Address } },
                    TtlInSeconds = 60,
                    TargetResourceId = resourceId,
                };
                await recordResource.UpdateAsync(data);
                return DnsUpdateResult.Good;
            }
        }
        // the record cannot be found
        else
        {
            return DnsUpdateResult.Nohost;
        }
    }

    public async Task<DnsUpdateResult> UpdateDnsV6Async(string hostname, IPAddress v6Address)
    {
        if (v6Address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            throw new ArgumentOutOfRangeException(nameof(v6Address));
        }

        var response = await _zoneResource.GetDnsAaaaRecordAsync(hostname);
        // is it an existing record?
        if (response.HasValue)
        {
            DnsAaaaRecordResource recordResource = response.Value;
            // the value is the same
            if (recordResource.Data.DnsAaaaRecords.Select(record => record.IPv6Address).Single().Equals(v6Address))
            {
                return DnsUpdateResult.Nochg;
            }
            // or the value is different?
            else
            {
                var resourceId = recordResource.Id;
                var data = new DnsAaaaRecordData
                {
                    DnsAaaaRecords = { new DnsAaaaRecordInfo() { IPv6Address = v6Address } },
                    TtlInSeconds = 60,
                    TargetResourceId = resourceId,
                };
                await recordResource.UpdateAsync(data);
                return DnsUpdateResult.Good;
            }
        }
        // the record cannot be found
        else
        {
            return DnsUpdateResult.Nohost;
        }
    }
}