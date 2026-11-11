using AutoPartsWarehouseLab6.Benchmarks;

var baseDir = AppDomain.CurrentDomain.BaseDirectory;
var dataDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "bench-data"));
Directory.CreateDirectory(dataDir);
var smallDbPath = Path.Combine(dataDir, "lab6-bench-small.db");
var largeDbPath = Path.Combine(dataDir, "lab6-bench-large.db");
var csvPath = Path.Combine(dataDir, "results.csv");

var command = args.Length > 0 ? args[0] : "help";

switch (command)
{
    case "generate-small":
        {
            var result = DataGenerator.Generate(smallDbPath, 20);
            Console.WriteLine($"Згенеровано {result.TotalRows} рядків у {result.DbPath} ({result.LowStockRows} з критичним залишком).");
            break;
        }
    case "generate-large":
        {
            var result = DataGenerator.Generate(largeDbPath, 100_000);
            Console.WriteLine($"Згенеровано {result.TotalRows} рядків у {result.DbPath} ({result.LowStockRows} з критичним залишком).");
            break;
        }
    case "benchmark":
        {
            if (!File.Exists(smallDbPath) || !File.Exists(largeDbPath))
            {
                Console.WriteLine("Немає згенерованих баз. Спочатку виконайте: generate-small та generate-large.");
                return 1;
            }

            BenchmarkRunner.WarmUpThreadPool();
            BenchmarkRunner.PrintEnvironment();
            var small = BenchmarkRunner.RunDataset("20 записів", smallDbPath);
            var large = BenchmarkRunner.RunDataset("100000 записів", largeDbPath);
            BenchmarkRunner.PrintTable(small, large);
            BenchmarkRunner.WriteCsv(csvPath, small, large);
            Console.WriteLine();
            Console.WriteLine($"Результати збережено у {csvPath}");

            if (!small.AllStrategiesAgree || !large.AllStrategiesAgree)
            {
                Console.WriteLine("ПОМИЛКА: стратегії повернули різні результати - дивіться вище.");
                return 1;
            }
            break;
        }
    case "selftest":
        return SelfTest.Run(dataDir);
    default:
        Console.WriteLine("Використання:");
        Console.WriteLine("  dotnet run -- generate-small    створити тестову базу з 20 запчастинами");
        Console.WriteLine("  dotnet run -- generate-large    створити тестову базу з ~100000 запчастинами");
        Console.WriteLine("  dotnet run -- benchmark         порівняти 4 стратегії вибірки на обох базах");
        Console.WriteLine("  dotnet run -- selftest          перевірки коректності (однакові результати, порожня таблиця, відсутність дублікатів)");
        return 1;
}

return 0;
