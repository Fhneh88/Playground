using System.Globalization;
using System.IO.Compression;

// ── arg parsing ──────────────────────────────────────────────────────────────
string? filePath = null;
DateTimeOffset? since = null;
DateTimeOffset? until = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--since" && i + 1 < args.Length)
        since = DateTimeOffset.Parse(args[++i]);
    else if (args[i] == "--until" && i + 1 < args.Length)
        until = DateTimeOffset.Parse(args[++i]);
    else
        filePath = args[i];
}

if (filePath is null)
{
    Console.Error.WriteLine("Usage: LogStats <access.log[.gz]> [--since yyyy-MM-dd] [--until yyyy-MM-dd]");
    return 1;
}

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    return 1;
}

// ── counters ─────────────────────────────────────────────────────────────────
var ipCount  = new Dictionary<string, int>();
var urlCount = new Dictionary<string, int>();
var status2xx = 0; var status3xx = 0; var status4xx = 0; var status5xx = 0;
int total = 0, skipped = 0;

// ── process lines ─────────────────────────────────────────────────────────────
using Stream raw = File.OpenRead(filePath);
using Stream stream = filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
    ? new GZipStream(raw, CompressionMode.Decompress)
    : raw;
using var reader = new StreamReader(stream);

string? line;
while ((line = reader.ReadLine()) is not null)
{
    var entry = TryParse(line);
    if (entry is null) { skipped++; continue; }

    if (since.HasValue && entry.Time < since.Value) continue;
    if (until.HasValue && entry.Time > until.Value) continue;

    total++;
    ipCount[entry.Ip]  = ipCount.GetValueOrDefault(entry.Ip)  + 1;
    string urlKey = $"{entry.Method} {entry.Url}";
    urlCount[urlKey]   = urlCount.GetValueOrDefault(urlKey) + 1;

    switch (entry.Status / 100)
    {
        case 2: status2xx++; break;
        case 3: status3xx++; break;
        case 4: status4xx++; break;
        case 5: status5xx++; break;
    }
}

// ── output ────────────────────────────────────────────────────────────────────
Console.WriteLine($"Всего запросов: {total}");
if (skipped > 0) Console.WriteLine($"(не удалось распарсить: {skipped})");

PrintTop("Топ IP:",  ipCount);
PrintTop("Топ URL:", urlCount);

Console.WriteLine("\nСтатус-коды:");
PrintStatus("2xx", status2xx, total);
PrintStatus("3xx", status3xx, total);
PrintStatus("4xx", status4xx, total);
PrintStatus("5xx", status5xx, total);

return 0;

// ── helpers ───────────────────────────────────────────────────────────────────
static void PrintTop(string header, Dictionary<string, int> dict)
{
    Console.WriteLine($"\n{header}");
    int maxKey = 0;
    int maxVal = 0;
    foreach (var kv in dict.OrderByDescending(kv => kv.Value).Take(5))
    {
        if (kv.Key.Length > maxKey) maxKey = kv.Key.Length;
        if (kv.Value > maxVal) maxVal = kv.Value;
    }
    int valWidth = maxVal.ToString().Length;

    foreach (var kv in dict.OrderByDescending(kv => kv.Value).Take(5))
        Console.WriteLine($"  {kv.Key.PadRight(maxKey)}  {kv.Value.ToString().PadLeft(valWidth)}");
}

static void PrintStatus(string label, int count, int total)
{
    if (count == 0) return;
    int pct = total > 0 ? count * 100 / total : 0;
    Console.WriteLine($"  {label}  {count,6} ({pct,2}%)");
}

// ── parser ────────────────────────────────────────────────────────────────────
static LogEntry? TryParse(string line)
{
    // IP
    int firstSpace = line.IndexOf(' ');
    if (firstSpace <= 0) return null;
    string ip = line.Substring(0, firstSpace);

    // Date: [dd/MMM/yyyy:HH:mm:ss +hhmm]
    int bracketOpen  = line.IndexOf('[', firstSpace);
    int bracketClose = line.IndexOf(']', bracketOpen + 1);
    if (bracketOpen < 0 || bracketClose < 0) return null;
    string dateStr = line.Substring(bracketOpen + 1, bracketClose - bracketOpen - 1);
    if (!DateTimeOffset.TryParseExact(dateStr, "dd/MMM/yyyy:HH:mm:ss zzz",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        return null;

    // Request: "METHOD /url HTTP/x.x"
    int quoteStart = line.IndexOf('"', bracketClose);
    int quoteEnd   = line.IndexOf('"', quoteStart + 1);
    if (quoteStart < 0 || quoteEnd < 0) return null;
    string request = line.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    var parts = request.Split(' ');
    if (parts.Length < 2) return null;

    // Status code
    int statusStart = quoteEnd + 2;
    int statusEnd   = line.IndexOf(' ', statusStart);
    if (statusEnd < 0) statusEnd = line.Length;
    if (!int.TryParse(line.AsSpan(statusStart, statusEnd - statusStart), out int status))
        return null;

    return new LogEntry(ip, parts[0], parts[1], status, time);
}

record LogEntry(string Ip, string Method, string Url, int Status, DateTimeOffset Time);
