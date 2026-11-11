using System.Net;
using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

// Перевіряє, що [Authorize(Roles=...)] справді блокує заборонені дії через реальний
// HTTP-конвеєр (routing -> authentication -> authorization), а не лише приховує посилання
// в інтерфейсі. TestDb.ActAs(), який використовують інші тести, навмисно не підходить для
// цього: він напряму створює контролер в обхід конвеєра авторизації.
[Trait("Category", "Roles")]
public class RoleAuthorizationHttpTests : IAsyncLifetime
{
    private WarehouseAppFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new WarehouseAppFactory();
        await _factory.SeedWorkerAsync();
        await _factory.SeedManagerAsync();
        await _factory.SeedAdministratorAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AnonymousRequest_ToStaffOnlyPage_IsRedirectedToLogin_NotToTheContent()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/AutoParts");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WarehouseWorker_PostingToManagerOnlyCreate_IsRejected_AndNoAutoPartIsCreated()
    {
        int partId;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<WarehouseContext>();
            var part = new AutoPart { Name = "Role Test Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
            seedContext.AutoParts.Add(part);
            await seedContext.SaveChangesAsync();
            partId = part.Id;
        }

        var client = await HttpAuthHelper.LoginAsAsync(_factory, WarehouseAppFactory.WorkerEmail, WarehouseAppFactory.TestPassword);
        // Воркер не має доступу до сторінки Create, але токен антипідробки прив'язаний до
        // сесії користувача, а не до конкретної адреси - бере його зі сторінки, яка воркеру доступна.
        var token = await HttpAuthHelper.GetAntiForgeryTokenAsync(client, $"/AutoParts/Issue/{partId}");
        var partName = "Role Test Part " + Guid.NewGuid();

        var response = await client.PostAsync("/AutoParts/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = partName,
            ["Manufacturer"] = "QA",
            ["MinimumStock"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
        Assert.False(await context.AutoParts.AnyAsync(part => part.Name == partName));
    }

    [Fact]
    public async Task Manager_PostingToWarehouseWorkerOnlyIssue_IsRejected_AndNoPartIssueIsCreated()
    {
        int partId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
            var part = new AutoPart { Name = "Role Test Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        var client = await HttpAuthHelper.LoginAsAsync(_factory, WarehouseAppFactory.ManagerEmail, WarehouseAppFactory.TestPassword);
        var token = await HttpAuthHelper.GetAntiForgeryTokenAsync(client, "/AutoParts/Create");

        var response = await client.PostAsync("/AutoParts/Issue", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["AutoPartId"] = partId.ToString(),
            ["Quantity"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<WarehouseContext>();
        Assert.False(await verifyContext.PartIssues.AnyAsync(issue => issue.AutoPartId == partId));
    }

    [Fact]
    public async Task Manager_RequestingAdministratorOnlyAdminIndex_IsRejected()
    {
        var client = await HttpAuthHelper.LoginAsAsync(_factory, WarehouseAppFactory.ManagerEmail, WarehouseAppFactory.TestPassword);

        var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WarehouseWorker_PostingToAdministratorOnlyUserCreation_IsRejected_AndNoUserIsCreated()
    {
        int partId;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<WarehouseContext>();
            var part = new AutoPart { Name = "Role Test Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
            seedContext.AutoParts.Add(part);
            await seedContext.SaveChangesAsync();
            partId = part.Id;
        }

        var client = await HttpAuthHelper.LoginAsAsync(_factory, WarehouseAppFactory.WorkerEmail, WarehouseAppFactory.TestPassword);
        var token = await HttpAuthHelper.GetAntiForgeryTokenAsync(client, $"/AutoParts/Issue/{partId}");
        var newEmail = $"forged-{Guid.NewGuid():N}@lab4.local";

        var response = await client.PostAsync("/Admin/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Forged User",
            ["Email"] = newEmail,
            ["Password"] = WarehouseAppFactory.TestPassword,
            ["Role"] = WarehouseRoles.Administrator
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
        Assert.False(await context.Users.AnyAsync(user => user.Email == newEmail));
    }
}
