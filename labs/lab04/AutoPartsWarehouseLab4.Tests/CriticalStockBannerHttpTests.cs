using System.Net;
using System.Text.RegularExpressions;
using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// End-to-end check (real TestServer, real Razor rendering) that the critical-stock banner from
/// UML ЛР1's "Повідомити менеджера про критичний залишок" use case actually shows up on the
/// page for Manager and links to the filtered catalog, and that it respects role boundaries -
/// it is a notification *to the manager*, not a general-purpose widget every role sees.
/// </summary>
public class CriticalStockBannerHttpTests : IAsyncLifetime
{
    private WarehouseAppFactory _factory = null!;
    private static readonly Regex TokenRegex = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

    public async Task InitializeAsync()
    {
        _factory = new WarehouseAppFactory();
        await _factory.SeedUsersAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<HttpClient> LoginAsAsync(string email)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var token = TokenRegex.Match(loginPage).Groups[1].Value;
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = WarehouseAppFactory.TestPassword,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task Manager_SeesCriticalStockBanner_LinkingToTheFilteredCatalog()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
            context.AutoParts.Add(new AutoPart { Name = "HTTP Banner Part", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 2 });
            await context.SaveChangesAsync();
        }

        var client = await LoginAsAsync(WarehouseAppFactory.ManagerEmail);
        var page = await client.GetStringAsync("/AutoParts");

        Assert.Contains("Критичні залишки", page);
        Assert.Contains("criticalOnly=true", page);
    }

    [Fact]
    public async Task Manager_SeesNoBanner_WhenNothingIsCritical()
    {
        // The app seeds demo data with a couple of deliberately-low parts on first run
        // (see SeedData.cs), so this test lifts every part above its own minimum first -
        // a genuinely "nothing critical" state - rather than asserting on an empty table
        // that Program.cs's own startup seeding never actually leaves behind.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
            foreach (var part in await context.AutoParts.ToListAsync())
            {
                part.QuantityInStock = part.MinimumStock + 10;
            }
            await context.SaveChangesAsync();
        }

        var client = await LoginAsAsync(WarehouseAppFactory.ManagerEmail);
        var page = await client.GetStringAsync("/AutoParts");

        Assert.DoesNotContain("critical-stock-banner", page);
    }

    [Fact]
    public async Task WarehouseWorker_DoesNotSeeTheManagerBanner_EvenWithCriticalStock()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
            context.AutoParts.Add(new AutoPart { Name = "Worker View Part", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 2 });
            await context.SaveChangesAsync();
        }

        var client = await LoginAsAsync(WarehouseAppFactory.WorkerEmail);
        var page = await client.GetStringAsync("/AutoParts");

        Assert.DoesNotContain("critical-stock-banner", page);
    }
}
