using ApplianceManager.Garage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApplianceManager.Daemon.Tests;

/// <summary>Boots the daemon in-process with the real `garage`-CLI-backed client swapped for a fake.</summary>
public sealed class ApplianceManagerWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeGarageClient Garage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGarageClient>();
            services.AddSingleton<IGarageClient>(Garage);
        });
    }
}
