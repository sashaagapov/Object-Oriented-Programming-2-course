using System.Security.Claims;
using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>No-op TempData backing store: the controllers under test only write to TempData, never read it back within the same request, so nothing needs to persist across requests here.</summary>
file sealed class NullTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

/// <summary>
/// Creates isolated, file-based SQLite databases under the OS temp directory so tests
/// never touch the developer's real AutoPartsWarehouseLab4.db and exercise real
/// SQLite locking (needed for the concurrency tests).
/// </summary>
public static class TestDb
{
    public static string CreateNewDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"AutoPartsWarehouseLab4.Tests.{Guid.NewGuid():N}.db");
        using var context = Open(path);
        context.Database.Migrate();
        return path;
    }

    public static WarehouseContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<WarehouseContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new WarehouseContext(options);
    }

    public static void Delete(string path)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
        {
            var file = path + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>Attaches a fake authenticated user (claims only, no real sign-in) to a controller under test.</summary>
    public static void ActAs(Controller controller, string userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
    }
}
