using System.Net;
using System.Text.RegularExpressions;
using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "Consistency")]
public class ConsistencyHttpTests : IAsyncLifetime
{
    private WarehouseAppFactory _factory = null!;
    private static readonly Regex TokenRegex = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

    public async Task InitializeAsync()
    {
        _factory = new WarehouseAppFactory();
        await _factory.SeedWorkerAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NegativeQuantity_PostedDirectlyToIssue_IsRejectedByServerSideValidation_NotJustTheBrowser()
    {
        int partId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
            var part = new AutoPart { Name = "Consistency Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var loginToken = TokenRegex.Match(loginPage).Groups[1].Value;
        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = WarehouseAppFactory.WorkerEmail,
            ["Password"] = WarehouseAppFactory.TestPassword,
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var issuePage = await client.GetStringAsync($"/AutoParts/Issue/{partId}");
        var issueToken = TokenRegex.Match(issuePage).Groups[1].Value;

        // A real browser's <input min="1"> and jQuery Validate would block this client-side;
        // we skip the browser entirely and send the negative value straight over HTTP.
        var response = await client.PostAsync("/AutoParts/Issue", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = issueToken,
            ["AutoPartId"] = partId.ToString(),
            ["Quantity"] = "-7"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // re-renders the form with a validation error, does not redirect to success
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Майбутню видачу оформлено", body);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<WarehouseContext>();
        Assert.False(await verifyContext.PartIssues.AnyAsync(i => i.AutoPartId == partId));
    }

    [Fact]
    public async Task AdjustStockBelowZero_PostedDirectlyToAdjustStock_IsRejectedByServerSideValidation()
    {
        int partId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
            var part = new AutoPart { Name = "Consistency Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var loginToken = TokenRegex.Match(loginPage).Groups[1].Value;
        await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = WarehouseAppFactory.WorkerEmail,
            ["Password"] = WarehouseAppFactory.TestPassword,
            ["__RequestVerificationToken"] = loginToken
        }));

        var adjustPage = await client.GetStringAsync($"/AutoParts/AdjustStock/{partId}");
        var adjustToken = TokenRegex.Match(adjustPage).Groups[1].Value;

        // UI не пропонує зменшити більше, ніж є на складі, але сервер не повинен довіряти клієнту.
        var response = await client.PostAsync("/AutoParts/AdjustStock", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adjustToken,
            ["AutoPartId"] = partId.ToString(),
            ["Delta"] = "-100",
            ["Reason"] = "Спроба обійти клієнтську перевірку"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // повторний рендер форми з помилкою, без редіректу на успіх

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<WarehouseContext>();
        Assert.Equal(5, (await verifyContext.AutoParts.FindAsync(partId))!.QuantityInStock);
        Assert.False(await verifyContext.StockAdjustments.AnyAsync(a => a.AutoPartId == partId));
    }
}
