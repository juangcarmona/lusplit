using Azure.Data.Tables;
using LuSplit.Functions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration["TableStorageConnectionString"]
        ?? "UseDevelopmentStorage=true";
    return new TableServiceClient(connectionString);
});

builder.Services.AddSingleton<GroupMetadataStore>();
builder.Services.AddSingleton<InvitationStore>();
builder.Services.AddSingleton<IDeviceStore, DeviceStore>();
builder.Services.AddSingleton<IKeyStore, KeyStore>();

builder.Build().Run();
