using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace Dash_analizer_logs
{
    class Program
    {
        private static readonly Regex _pattern = new Regex(
            @"^(?<time>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}) \[(?<level>\w+)\] \[(?<src>[^]]+)\] (?<msg>.+)$",
            RegexOptions.Compiled);

        static void Main(string[] args)
        {
            // Параметры: путь, --level=Error,Warning,Info, --ip=192.168.1.1, --csv=output.csv
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            var path = args[0];
            var levels = GetArgValues(args, "--level").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var ips = GetArgValues(args, "--ip").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var csvOut = GetArgValues(args, "--csv");

            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Файл не найден: {path}");
                Console.ResetColor();
                return;
            }

            var entries = ParseLogFile(path)
                          .Where(e => (levels.Length == 0 || levels.Contains(e.Level, StringComparer.OrdinalIgnoreCase)))
                          .Where(e => (ips.Length == 0 || ips.Contains(e.Source)))
                          .ToList();

            if (!string.IsNullOrEmpty(csvOut))
                ExportToCsv(entries, csvOut);

            PrintSummary(entries);
        }

        static void PrintUsage()
        {
            Console.WriteLine("Использование: dotnet run <путь_к_файлу> [--level=Error,Warning] [--ip=1.2.3.4] [--csv=out.csv]");
        }

        static string GetArgValues(string[] args, string key)
        {
            var prefix = key + "=";
            var seg = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return seg != null ? seg.Substring(prefix.Length) : string.Empty;
        }

        static IEnumerable<LogEntry> ParseLogFile(string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                var m = _pattern.Match(line);
                if (!m.Success) continue;
                yield return new LogEntry
                {
                    Timestamp = DateTime.Parse(m.Groups["time"].Value),
                    Level = m.Groups["level"].Value,
                    Source = m.Groups["src"].Value,
                    Message = m.Groups["msg"].Value
                };
            }
        }

        static void ExportToCsv(IEnumerable<LogEntry> entries, string outPath)
        {
            using var w = new StreamWriter(outPath);
            w.WriteLine("Timestamp,Level,Source,Message");
            foreach (var e in entries)
                w.WriteLine($"\"{e.Timestamp:O}\",\"{e.Level}\",\"{e.Source}\",\"{e.Message.Replace("\"", "\"\"")}\"");
            Console.WriteLine($"Экспортировано {entries.Count()} записей в {outPath}");
        }

        static void PrintSummary(List<LogEntry> entries)
        {
            Console.ResetColor();
            Console.WriteLine($"\nВсего событий: {entries.Count}");

            var byLevel = entries
                .GroupBy(e => e.Level)
                .Select(g => (Level: g.Key, Count: g.Count()));
            Console.WriteLine("По уровням:");
            foreach (var (lvl, cnt) in byLevel)
                Console.WriteLine($"  {lvl}: {cnt}");

            var topIPs = entries
                .GroupBy(e => e.Source)
                .OrderByDescending(g => g.Count())
                .Take(5);
            Console.WriteLine("Топ-5 IP/источников:");
            foreach (var g in topIPs)
                Console.WriteLine($"  {g.Key}: {g.Count()}");
        }
    }

    class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
    }
}
