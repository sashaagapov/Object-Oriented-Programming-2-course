using System.Collections.Concurrent;
using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouseLab6.Benchmarks;

/// <summary>
/// The one business query under comparison: find AutoPart rows that need reordering
/// (QuantityInStock &lt;= MinimumStock). Every strategy below reads from SQLite for every
/// partition - none of them filters an already-loaded in-memory array - so the comparison
/// measures real parallel database access, not in-memory parallel filtering.
/// </summary>
public static class QueryService
{
    public static WarehouseContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<WarehouseContext>()
            .UseSqlite(connectionString)
            .Options;
        return new WarehouseContext(options);
    }

    /// <summary>Id range of the AutoParts table, fetched once outside any timed measurement.</summary>
    public static (int MinId, int MaxId, int RowCount) GetIdRange(string connectionString)
    {
        using var context = NewContext(connectionString);
        if (!context.AutoParts.Any()) return (0, -1, 0);
        var minId = context.AutoParts.Min(p => p.Id);
        var maxId = context.AutoParts.Max(p => p.Id);
        var count = context.AutoParts.Count();
        return (minId, maxId, count);
    }

    private static List<(int Lo, int Hi)> Partitions(int minId, int maxId, int degree)
    {
        var partitions = new List<(int Lo, int Hi)>();
        if (maxId < minId) return partitions; // empty table

        var total = maxId - minId + 1;
        var size = (int)Math.Ceiling(total / (double)degree);
        for (var lo = minId; lo <= maxId; lo += size)
        {
            var hi = Math.Min(lo + size - 1, maxId);
            partitions.Add((lo, hi));
        }
        return partitions;
    }

    private static List<int> LowStockIdsInRange(string connectionString, int lo, int hi)
    {
        using var context = NewContext(connectionString);
        return context.AutoParts
            .Where(part => part.Id >= lo && part.Id <= hi && part.QuantityInStock <= part.MinimumStock)
            .Select(part => part.Id)
            .ToList();
    }

    /// <summary>Strategy 1: one ordinary EF Core query, no parallelism at all.</summary>
    public static List<int> Sequential(string connectionString)
    {
        using var context = NewContext(connectionString);
        return context.AutoParts
            .AsNoTracking()
            .Where(part => part.QuantityInStock <= part.MinimumStock)
            .Select(part => part.Id)
            .ToList();
    }

    /// <summary>Strategy 2: manual System.Threading.Thread per Id-range partition, each with its own DbContext.</summary>
    public static List<int> ManualThreads(string connectionString, int minId, int maxId, int degree)
    {
        var partitions = Partitions(minId, maxId, degree);
        var results = new List<int>[partitions.Count];
        var threads = new Thread[partitions.Count];

        for (var i = 0; i < partitions.Count; i++)
        {
            var index = i;
            var (lo, hi) = partitions[i];
            threads[i] = new Thread(() => { results[index] = LowStockIdsInRange(connectionString, lo, hi); });
            threads[i].Start();
        }
        foreach (var thread in threads) thread.Join();

        return results.SelectMany(r => r).ToList();
    }

    /// <summary>Strategy 3: Task Parallel Library (Parallel.ForEach) with an explicit degree of parallelism.</summary>
    public static List<int> Tpl(string connectionString, int minId, int maxId, int degree)
    {
        var partitions = Partitions(minId, maxId, degree);
        var bag = new ConcurrentBag<int>();

        Parallel.ForEach(
            partitions,
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            partition =>
            {
                foreach (var id in LowStockIdsInRange(connectionString, partition.Lo, partition.Hi))
                    bag.Add(id);
            });

        return bag.ToList();
    }

    /// <summary>Strategy 4: PLINQ with an explicit degree of parallelism (multi-core query over disjoint Id ranges).</summary>
    public static List<int> Plinq(string connectionString, int minId, int maxId, int degree)
    {
        var partitions = Partitions(minId, maxId, degree);
        return partitions
            .AsParallel()
            .WithDegreeOfParallelism(Math.Max(1, Math.Min(degree, partitions.Count)))
            .SelectMany(partition => LowStockIdsInRange(connectionString, partition.Lo, partition.Hi))
            .ToList();
    }
}
