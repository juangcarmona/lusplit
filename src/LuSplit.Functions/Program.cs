using Azure.Data.Tables;
using Azure.Identity;
using LuSplit.Functions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var tableServiceUri = Environment.GetEnvironmentVariable("AzureWebJobsStorage__tableServiceUri");
        var connectionString = Environment.GetEnvironmentVariable("TableStorageConnectionString");

        TableServiceClient tableServiceClient;

        if (!string.IsNullOrWhiteSpace(tableServiceUri))
        {
            tableServiceClient = new TableServiceClient(
                new Uri(tableServiceUri),
                new DefaultAzureCredential());
        }
        else if (!string.IsNullOrWhiteSpace(connectionString))
        {
            tableServiceClient = new TableServiceClient(connectionString);
        }
        else
        {
            throw new InvalidOperationException(
                "Table storage is not configured. Set AzureWebJobsStorage__tableServiceUri or TableStorageConnectionString.");
        }

        services.AddSingleton(tableServiceClient);
        services.AddSingleton<IGroupMetadataStore, GroupMetadataStore>();
        services.AddSingleton<IInvitationStore, InvitationStore>();
        services.AddSingleton<IDeviceStore, DeviceStore>();
        services.AddSingleton<IKeyStore, KeyStore>();
    })
    .Build();

host.Run();