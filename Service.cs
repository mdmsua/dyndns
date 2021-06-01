using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Microsoft.Extensions.Logging;

class Service
{
    private readonly string resourceGroupName;
    private readonly string zoneName;
    private readonly Options options;
    private readonly DnsManagementClient client;
    private readonly ILogger<Service> logger;
    private const int ttl = 3600;

    public Service(Options options, DnsManagementClient client, ILogger<Service> logger)
    {
        this.resourceGroupName = options.ResourceGroupName;
        this.zoneName = options.ZoneName;
        this.options = options;
        this.client = client;
        this.logger = logger;
    }

    internal async Task CreateOrUpdateRecordSetAsync(string? name, string? ipv4, string? ipv6, string? ua, string? id, string? ip)
    {
        var v4 = ipv4?.Trim();
        var v6 = ipv6?.Trim();

        List<Task> tasks = new(2);

        if (!string.IsNullOrEmpty(v4))
        {
            if (IPAddress.TryParse(v4, out _))
            {
                var recordSet = GetRecordSet(ua, id, ip);
                recordSet.ARecords.Add(new ARecord { Ipv4Address = v4 });
                var task = client.RecordSets.CreateOrUpdateAsync(resourceGroupName, zoneName, name, RecordType.A, recordSet).ContinueWith(task => Continuation(task)); 
                tasks.Add(task);
            }
            else
            {
                logger.LogWarning(Events.ParseFailed, "{0} is not a valid IPv4", v4);
            }
        }

        if (!string.IsNullOrEmpty(v6))
        {
            if (IPAddress.TryParse(v6, out _))
            {
                var recordSet = GetRecordSet(ua, id, ip);
                recordSet.AaaaRecords.Add(new AaaaRecord { Ipv6Address = v6 });
                var task = client.RecordSets.CreateOrUpdateAsync(resourceGroupName, zoneName, name, RecordType.Aaaa, recordSet).ContinueWith(task => Continuation(task)); 
                tasks.Add(task);
            }
            else
            {
                logger.LogWarning(Events.ParseFailed, "{0} is not a valid IPv6", v6);
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private static RecordSet GetRecordSet(string? ua, string? id, string? ip)
    {
        var ts = DateTime.UtcNow.ToString("s");

        RecordSet recordSet = new() { TTL = ttl };
        recordSet.Metadata[nameof(ua)] = ua;
        recordSet.Metadata[nameof(id)] = id;
        recordSet.Metadata[nameof(ip)] = ip;
        recordSet.Metadata[nameof(ts)] = ts;

        return recordSet;
    }

    private Task Continuation(Task<Response<RecordSet>> task)
    {
        if (task.IsFaulted)
        {
            logger.LogError(Events.SyncFailed,  task.Exception, "Failed to synchronize record set");
        }
        else if (task.IsCanceled)
        {
            logger.LogWarning(Events.SyncCancelled, "Record set synchronization was cancelled");
        }
        else
        {
            RecordSet recordSet = task.Result.Value;
            string type = recordSet.Type[(recordSet.Type.LastIndexOf("/")+1)..];
            string? GetAddress(RecordSet set, string type) => type switch
            {
                "A" => set.ARecords?.SingleOrDefault<ARecord>()?.Ipv4Address,
                "AAAA" => set.AaaaRecords?.SingleOrDefault<AaaaRecord>()?.Ipv6Address,
                _ => default
            };
            logger.LogInformation(Events.SyncCompleted, "Successfully synchronized {0} record set {1}: {2}", type, recordSet.Fqdn, GetAddress(recordSet, type));
        }

        return Task.CompletedTask;
    }

    internal static class Events
    {
        internal static readonly EventId SyncCompleted = 11;
        internal static readonly EventId SyncCancelled = 12;
        internal static readonly EventId SyncFailed = 13;
        internal static readonly EventId ParseFailed = 1;
    }
}