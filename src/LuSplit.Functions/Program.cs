using Azure.Data.Tables;
using LuSplit.Functions.Middleware;
using LuSplit.Functions.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration["TableStorageConnectionString"]
        ?? "UseDevelopmentStorage=true";
    return new TableServiceClient(connectionString);
});

builder.Services.AddSingleton<GroupMetadataStore>();
builder.Services.AddSingleton<InvitationStore>();

builder.UseMiddleware<EntraTokenValidationMiddleware>();

builder.Build().Run();
