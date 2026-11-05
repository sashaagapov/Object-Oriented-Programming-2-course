using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Section 9 of the audit: migrations must apply cleanly to a brand-new database, must be a
/// safe no-op when re-applied to an already-migrated database, must preserve existing data
/// (including the legacy issue rows converted by the 2026-11-05 migrations), and must never
/// touch the developer's real database file directly - everything here runs against a
/// throwaway copy made under the OS temp directory.
/// </summary>
public class DatabaseMigrationTests
{
    private static string? RealDatabasePath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AutoPartsWarehouseLab4", "AutoPartsWarehouseLab4.db");
        path = Path.GetFullPath(path);
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public void FreshDatabase_AppliesAllMigrations_WithoutError()
    {
        var path = TestDb.CreateNewDatabasePath(); // throws if Migrate() fails
        try
        {
            using var context = TestDb.Open(path);
            Assert.True(context.Database.GetAppliedMigrations().Count() >= 10);
            Assert.Empty(context.Database.GetPendingMigrations());
        }
        finally
        {
            TestDb.Delete(path);
        }
    }

    [Fact]
    public void ReMigratingAnAlreadyCurrentDatabase_IsANoOp()
    {
        var path = TestDb.CreateNewDatabasePath();
        try
        {
            using var context = TestDb.Open(path);
            context.Database.Migrate(); // already current - must not throw or duplicate anything
            Assert.Empty(context.Database.GetPendingMigrations());
        }
        finally
        {
            TestDb.Delete(path);
        }
    }

    [Fact]
    public void MigratingACopyOfTheRealDeveloperDatabase_PreservesHistoryAndPassesIntegrityChecks()
    {
        var sourcePath = RealDatabasePath();
        if (sourcePath == null)
        {
            // No real database present in this environment (e.g. CI) - nothing unsafe to verify here.
            return;
        }

        var copyPath = Path.Combine(Path.GetTempPath(), $"AutoPartsWarehouseLab4.RealCopy.{Guid.NewGuid():N}.db");
        File.Copy(sourcePath, copyPath);
        try
        {
            int issueCountBefore;
            using (var before = TestDb.Open(copyPath))
            {
                issueCountBefore = before.PartIssues.Count();
            }

            using (var context = TestDb.Open(copyPath))
            {
                context.Database.Migrate(); // must be a safe no-op: this copy is already fully migrated
            }

            using var after = TestDb.Open(copyPath);
            Assert.Equal(issueCountBefore, after.PartIssues.Count());
            Assert.Empty(after.Database.GetPendingMigrations());

            var integrity = after.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToList();
            Assert.Equal(new List<string> { "ok" }, integrity);
        }
        finally
        {
            foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
            {
                var file = copyPath + suffix;
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
