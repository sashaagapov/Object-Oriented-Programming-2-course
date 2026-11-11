namespace AutoPartsWarehouseLab6.Benchmarks;

/// <summary>
/// Automated correctness checks for the data generator and the four query strategies.
/// Run via: dotnet run -- selftest
/// </summary>
public static class SelfTest
{
    public static int Run(string dataDir)
    {
        var failures = new List<string>();
        void Check(bool condition, string description)
        {
            Console.WriteLine($"[{(condition ? "OK" : "FAIL")}] {description}");
            if (!condition) failures.Add(description);
        }

        var lab05DbPath = Path.GetFullPath(Path.Combine(dataDir, "..", "..", "..", "lab05",
            "AutoPartsWarehouseLab5", "AutoPartsWarehouseLab5.db"));
        var lab05Exists = File.Exists(lab05DbPath);
        var lab05MTimeBefore = lab05Exists ? File.GetLastWriteTimeUtc(lab05DbPath) : (DateTime?)null;

        // 1) Empty table: all strategies must agree (and return nothing) without throwing.
        var emptyDbPath = Path.Combine(dataDir, "lab6-bench-selftest-empty.db");
        DataGenerator.Generate(emptyDbPath, 0);
        var emptyOutcome = BenchmarkRunnerLite.RunOnce(emptyDbPath);
        Check(emptyOutcome.AllAgree, "Порожня таблиця: усі 4 стратегії погоджуються (і не падають)");
        Check(emptyOutcome.ResultCount == 0, "Порожня таблиця: 0 запчастин для поповнення");

        // 2) Small dataset (20 rows): all strategies must return the identical Id set.
        var smallDbPath = Path.Combine(dataDir, "lab6-bench-selftest-small.db");
        DataGenerator.Generate(smallDbPath, 20);
        var smallOutcome = BenchmarkRunnerLite.RunOnce(smallDbPath);
        Check(smallOutcome.AllAgree, "20 записів: усі 4 стратегії повертають однаковий набір Id");

        // 3) Larger dataset (2000 rows - enough to exercise multiple partitions without the full 100k cost here).
        var mediumDbPath = Path.Combine(dataDir, "lab6-bench-selftest-medium.db");
        DataGenerator.Generate(mediumDbPath, 2000);
        var mediumOutcome = BenchmarkRunnerLite.RunOnce(mediumDbPath);
        Check(mediumOutcome.AllAgree, "2000 записів: усі 4 стратегії повертають однаковий набір Id");

        // 4) Regenerating the same size must not accumulate duplicates - row count stays exactly the target.
        DataGenerator.Generate(smallDbPath, 20);
        DataGenerator.Generate(smallDbPath, 20);
        var rowCountAfterRegen = QueryService.GetIdRange(DataGenerator.ConnectionString(smallDbPath)).RowCount;
        Check(rowCountAfterRegen == 20, $"Повторна генерація 20 записів не накопичує дублікатів (отримано {rowCountAfterRegen})");

        // 5) The web app's and lab05's working databases must stay untouched by anything above.
        Check(!lab05Exists || File.GetLastWriteTimeUtc(lab05DbPath) == lab05MTimeBefore,
            "Робоча база ЛР5 (AutoPartsWarehouseLab5.db) не змінена під час роботи ЛР6" +
            (lab05Exists ? "" : " (файл відсутній - перевірку часу зміни пропущено)"));

        foreach (var path in new[] { emptyDbPath, smallDbPath, mediumDbPath })
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        Console.WriteLine();
        Console.WriteLine(failures.Count == 0
            ? "Усі перевірки коректності пройшли."
            : $"ПОМИЛКИ ({failures.Count}): {string.Join("; ", failures)}");
        return failures.Count == 0 ? 0 : 1;
    }
}

internal static class BenchmarkRunnerLite
{
    public sealed record Outcome(bool AllAgree, int ResultCount);

    public static Outcome RunOnce(string dbPath)
    {
        var connectionString = DataGenerator.ConnectionString(dbPath);
        var (minId, maxId, _) = QueryService.GetIdRange(connectionString);

        var sequential = QueryService.Sequential(connectionString).ToHashSet();
        var threads = QueryService.ManualThreads(connectionString, minId, maxId, BenchmarkRunner.DegreeOfParallelism).ToHashSet();
        var tpl = QueryService.Tpl(connectionString, minId, maxId, BenchmarkRunner.DegreeOfParallelism).ToHashSet();
        var plinq = QueryService.Plinq(connectionString, minId, maxId, BenchmarkRunner.DegreeOfParallelism).ToHashSet();

        var allAgree = sequential.SetEquals(threads) && sequential.SetEquals(tpl) && sequential.SetEquals(plinq);
        return new Outcome(allAgree, sequential.Count);
    }
}
