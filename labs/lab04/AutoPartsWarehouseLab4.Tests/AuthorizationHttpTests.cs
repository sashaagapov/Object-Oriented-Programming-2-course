using System.Net;
using System.Text.RegularExpressions;
using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Drives the real HTTP pipeline (TestServer) to check that role restrictions, self-service
/// blocks and antiforgery protection are enforced server-side, not just by hiding buttons in
/// the Razor views.
/// </summary>
public class AuthorizationHttpTests : IAsyncLifetime
{
    private WarehouseAppFactory _factory = null!;
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        _factory = new WarehouseAppFactory();
        await _factory.SeedUsersAsync();
        _anonymous = _factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static readonly Regex TokenRegex = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

    private async Task<(HttpClient client, string antiforgeryToken)> LoginAsAsync(string email, string tokenSourcePath = "/AutoParts")
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var token = TokenRegex.Match(loginPage).Groups[1].Value;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = WarehouseAppFactory.TestPassword,
            ["__RequestVerificationToken"] = token
        });
        var response = await client.PostAsync("/Account/Login", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); // successful login redirects, it does not re-render the form

        // A fresh antiforgery token tied to the now-authenticated cookie, for later POSTs.
        // Must be a page this role is actually allowed to view (a 302 to AccessDenied has no form/token).
        var afterLoginResponse = await client.GetAsync(tokenSourcePath);
        Assert.Equal(HttpStatusCode.OK, afterLoginResponse.StatusCode);
        var afterLoginPage = await afterLoginResponse.Content.ReadAsStringAsync();
        var authedToken = TokenRegex.Match(afterLoginPage).Groups[1].Value;
        return (client, authedToken);
    }

    [Fact]
    public async Task AnonymousUser_IsRedirectedToLogin()
    {
        var response = await _anonymous.GetAsync("/AutoParts");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AnonymousPost_ToStateChangingAction_IsRedirectedToLogin_NotExecuted()
    {
        var response = await _anonymous.PostAsync("/AutoParts/CompleteIssue/1", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WarehouseWorker_CannotConfirmPayment_ServerRejectsEvenWithDirectPost()
    {
        var (client, token) = await LoginAsAsync(WarehouseAppFactory.WorkerEmail);
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
        var response = await client.PostAsync("/AutoParts/ConfirmPayment/1", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Manager_CannotReceivePurchaseOrder_ServerRejects()
    {
        var (client, token) = await LoginAsAsync(WarehouseAppFactory.ManagerEmail);
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
        var response = await client.PostAsync("/PurchaseOrders/Receive/1", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WarehouseWorker_CannotCreateAutoPart_ServerRejects()
    {
        var (client, token) = await LoginAsAsync(WarehouseAppFactory.WorkerEmail);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Спроба обходу ролі",
            ["Manufacturer"] = "QA",
            ["MinimumStock"] = "1"
        });
        var response = await client.PostAsync("/AutoParts/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WarehouseContext>();
        Assert.False(await context.AutoParts.AnyAsync(p => p.Name == "Спроба обходу ролі"));
    }

    [Fact]
    public async Task Administrator_CannotChangeOwnRole()
    {
        var (client, token) = await LoginAsAsync(WarehouseAppFactory.AdminEmail, "/Admin/Create");
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(WarehouseAppFactory.AdminEmail);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = admin!.Id,
            ["role"] = WarehouseRoles.Manager
        });
        var response = await client.PostAsync("/Admin/ChangeRole", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await users.IsInRoleAsync(admin, WarehouseRoles.Administrator));
    }

    [Fact]
    public async Task Administrator_CannotLockOwnAccount()
    {
        var (client, token) = await LoginAsAsync(WarehouseAppFactory.AdminEmail, "/Admin/Create");
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(WarehouseAppFactory.AdminEmail);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = admin!.Id,
            ["locked"] = "true"
        });
        var response = await client.PostAsync("/Admin/SetLocked", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await users.IsLockedOutAsync(admin));
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejected_EvenForAuthorizedRole()
    {
        var (client, _) = await LoginAsAsync(WarehouseAppFactory.ManagerEmail);
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "QA Supplier", ["Phone"] = "+380000000000" });
        var response = await client.PostAsync("/Suppliers/Create", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BlockedUser_LosesAccessImmediately_WithoutWaitingForSessionExpiry()
    {
        var (client, _) = await LoginAsAsync(WarehouseAppFactory.WorkerEmail);
        var beforeBlock = await client.GetAsync("/AutoParts");
        Assert.Equal(HttpStatusCode.OK, beforeBlock.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var worker = await users.FindByEmailAsync(WarehouseAppFactory.WorkerEmail);
            await users.SetLockoutEnabledAsync(worker!, true);
            await users.SetLockoutEndDateAsync(worker!, DateTimeOffset.MaxValue);
            await users.UpdateSecurityStampAsync(worker!);
        }

        var afterBlock = await client.GetAsync("/AutoParts"); // same cookie/session, no new login
        Assert.Equal(HttpStatusCode.Redirect, afterBlock.StatusCode);
        Assert.Contains("/Account/Login", afterBlock.Headers.Location!.ToString());
    }
}
