using Azure.Identity;
using Azure.ResourceManager.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton(provider =>
{
    var environment = provider.GetRequiredService<IHostEnvironment>();
    var options = provider.GetRequiredService<Options>();
    return new DnsManagementClient(options.SubscriptionId, environment.IsProduction() ? new ManagedIdentityCredential() : new DefaultAzureCredential());
});
builder.Services.AddSingleton<Service>();
builder.Services.AddSingleton(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new Options(configuration.GetValue<string>(nameof(Options.SubscriptionId)), configuration.GetValue<string>(nameof(Options.ResourceGroupName)), configuration.GetValue<string>(nameof(Options.ZoneName)));
});

await using var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHealthChecks("/health");

app.MapGet("/", async context =>
{
    string? name = context.Request.Query["name"];
    string? ipv4 = context.Request.Query["ipv4"];
    string? ipv6 = context.Request.Query["ipv6"];
    string? ua = context.Request.Headers[HeaderNames.UserAgent];
    string? id = context.Request.HttpContext.Connection.Id;
    string? ip = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
    await context.RequestServices.GetRequiredService<Service>().CreateOrUpdateRecordSetAsync(name, ipv4, ipv6, ua, id, ip);
});

await app.RunAsync();
