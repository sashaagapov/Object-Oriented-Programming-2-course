using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoPartsWarehouseLab6.Benchmarks;

public static class BenchmarkRunner
{
    public const int DegreeOfParallelism = 4;
    private const int Repetitions = 9; // odd & >=5: a resilient median even if one rep hits a GC/scheduler stall

    public static readonly string[] StrategyNames = { "Sequential", "Manual Threads", "TPL", "Multi-core / PLINQ" };

    public sealed record StrategyOutcome(string Name, double MedianMs, List<double> AllMs, HashSet<int> ResultIds);

    public sealed record DatasetOutcome(string Label, int RowCount, int LowStockRows, List<StrategyOutcome> Strategies, bool AllStrategiesAgree);

    private static List<int> Run(string strategy, string connectionString, int minId, int maxId) => strategy switch
    {
        "Sequential" => QueryService.Sequential(connectionString),
        "Manual Threads" => QueryService.ManualThreads(connectionString, minId, maxId, DegreeOfParallelism),
        "TPL" => QueryService.Tpl(connectionString, minId, maxId, DegreeOfParallelism),
        "Multi-core / PLINQ" => QueryService.Plinq(connectionString, minId, maxId, DegreeOfParallelism),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    /// <summary>
    /// Times every strategy against the database at <paramref name="dbPath"/>: one untimed warm-up
    /// run followed by <see cref="Repetitions"/> timed repetitions per strategy, reporting the median.
    /// Also verifies every strategy returns the identical set of AutoPart Ids.
    /// </summary>
    public static DatasetOutcome RunDataset(string label, string dbPath)
    {
        var connectionString = DataGenerator.ConnectionString(dbPath);
        var (minId, maxId, rowCount) = QueryService.GetIdRange(connectionString);

        var outcomes = new List<StrategyOutcome>();
        foreach (var strategy in StrategyNames)
        {
            // Warm-up (3x, not 1x): the .NET ThreadPool grows slowly under its default thread-injection
            // throttle, so Parallel.ForEach/PLINQ can otherwise look falsely slow on their very first uses
            // in the process. These runs also prime the OS page cache, the SQLite connection-open path and
            // JIT-compile the LINQ query - none of this is part of the measured interval.
            for (var warmup = 0; warmup < 3; warmup++)
                Run(strategy, connectionString, minId, maxId);

            var timings = new List<double>();
            HashSet<int>? ids = null;
            for (var rep = 0; rep < Repetitions; rep++)
            {
                // A clean slate before each timed repetition keeps a stop-the-world GC collection
                // triggered by a PREVIOUS repetition's garbage from landing inside THIS measurement.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var stopwatch = Stopwatch.StartNew();
                var result = Run(strategy, connectionString, minId, maxId);
                stopwatch.Stop();
                timings.Add(stopwatch.Elapsed.TotalMilliseconds);
                ids ??= result.ToHashSet(); // identical dataset/query every repetition, so capture once
            }

            outcomes.Add(new StrategyOutcome(strategy, Median(timings), timings, ids ?? new HashSet<int>()));
        }

        var reference = outcomes[0].ResultIds;
        var allAgree = outcomes.All(o => o.ResultIds.SetEquals(reference));
        var lowStockRows = reference.Count;

        return new DatasetOutcome(label, rowCount, lowStockRows, outcomes, allAgree);
    }

    /// <summary>
    /// Raises the ThreadPool's minimum worker count to the degree of parallelism up front, so the
    /// pool's default slow thread-injection rate does not masquerade as "parallelism is slow" on the
    /// first few Parallel.ForEach/PLINQ calls. This affects how fast the pool reaches a given size,
    /// not how many AutoParts rows satisfy the business query.
    /// </summary>
    public static void WarmUpThreadPool()
    {
        ThreadPool.SetMinThreads(DegreeOfParallelism + 2, DegreeOfParallelism + 2);
    }

    public static void PrintEnvironment()
    {
        Console.WriteLine("Середовище вимірювання:");
        Console.WriteLine($"  .NET: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  ОС: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        Console.WriteLine($"  Логічних процесорів: {Environment.ProcessorCount}");
        Console.WriteLine($"  Ступінь паралелізму (DOP): {DegreeOfParallelism}");
        Console.WriteLine($"  Повторів на стратегію: {Repetitions} (+ 3 розігрівальних виклики, не враховуються)");
        Console.WriteLine();
    }

    public static void PrintTable(DatasetOutcome small, DatasetOutcome large)
    {
        Console.WriteLine($"{"Спосіб",-20} | {small.Label + ", мс",18} | {large.Label + ", мс",18} | Прискорення на {large.Label}");
        Console.WriteLine(new string('-', 90));
        var sequentialLarge = large.Strategies.First(s => s.Name == "Sequential").MedianMs;
        foreach (var name in StrategyNames)
        {
            var s = small.Strategies.First(x => x.Name == name);
            var l = large.Strategies.First(x => x.Name == name);
            var speedup = sequentialLarge / l.MedianMs;
            Console.WriteLine($"{name,-20} | {s.MedianMs,18:F2} | {l.MedianMs,18:F2} | {speedup,10:F2}x");
        }
        Console.WriteLine();
        Console.WriteLine($"Правильність результатів: {small.Label} -> {(small.AllStrategiesAgree ? "усі стратегії збіглися" : "РОЗБІЖНІСТЬ!")}, " +
                           $"{large.Label} -> {(large.AllStrategiesAgree ? "усі стратегії збіглися" : "РОЗБІЖНІСТЬ!")}");
        Console.WriteLine($"Знайдено запчастин для поповнення: {small.Label} = {small.LowStockRows} з {small.RowCount}, " +
                           $"{large.Label} = {large.LowStockRows} з {large.RowCount}");
    }

    public static void WriteCsv(string csvPath, DatasetOutcome small, DatasetOutcome large)
    {
        var sequentialLarge = large.Strategies.First(s => s.Name == "Sequential").MedianMs;
        using var writer = new StreamWriter(csvPath, append: false);
        writer.WriteLine("Strategy,DatasetLabel,RowCount,LowStockRows,MedianMs,AllTimingsMs,SpeedupVsSequentialAtLarge");
        foreach (var dataset in new[] { small, large })
        {
            foreach (var s in dataset.Strategies)
            {
                var speedup = dataset.Label == large.Label ? sequentialLarge / s.MedianMs : double.NaN;
                var allTimings = string.Join(";", s.AllMs.Select(v => v.ToString("F3")));
                writer.WriteLine($"{s.Name},{dataset.Label},{dataset.RowCount},{dataset.LowStockRows},{s.MedianMs:F3},{allTimings},{speedup:F3}");
            }
        }
    }
}
