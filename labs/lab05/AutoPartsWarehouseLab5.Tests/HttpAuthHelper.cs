using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

public static class HttpAuthHelper
{
    private static readonly Regex TokenRegex = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

    public static async Task<HttpClient> LoginAsAsync(WarehouseAppFactory factory, string email, string password)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var loginToken = TokenRegex.Match(loginPage).Groups[1].Value;
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string pageUrl)
    {
        var page = await client.GetStringAsync(pageUrl);
        return TokenRegex.Match(page).Groups[1].Value;
    }
}
