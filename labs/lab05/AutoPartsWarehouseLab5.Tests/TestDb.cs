using System.Security.Claims;
using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouseLab5.Tests;

public static class TestDb
{
    public static string CreateNewDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"AutoPartsWarehouseLab5.Tests.{Guid.NewGuid():N}.db");
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

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    public static void ActAs(Controller controller, string userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
    }
}
