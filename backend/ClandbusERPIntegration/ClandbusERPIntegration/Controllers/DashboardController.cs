using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClandbusERPIntegration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController(
    IDbContextFactory<DashboardDbContext> dbFactory,
    ISynchronizationService synchronization,
    IAcumaticaSessionStore sessions,
    IWebHostEnvironment environment) : ControllerBase
{
    private IAcumaticaService? Current => sessions.GetCurrent(HttpContext);
    private string UserKey => Current!.CurrentUsername.Trim().ToLowerInvariant();
    private string TaskImportFolder => Path.Combine(
        environment.ContentRootPath, "App_Data", "TaskImports");

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var service = Current;
        if (service is null) return Unauthorized();
        var result = await synchronization.SynchronizeAsync(service, cancellationToken);
        return Ok(new { result.CasesRead, result.TasksRead, result.FinishedAt });
    }

    [HttpPost("import-tasks")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> ImportTasks(IFormFile file, CancellationToken cancellationToken)
    {
        if (Current is null)
            return Unauthorized(new { message = "Inicia sesión para identificar al propietario de las Tasks." });
        if (file.Length == 0 || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Selecciona el archivo .xlsx exportado desde la pantalla Tareas de Acumatica." });

        IReadOnlyList<Dictionary<string, string>> rows;
        try
        {
            Directory.CreateDirectory(TaskImportFolder);
            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            safeName = Regex.Replace(safeName, @"[^a-zA-Z0-9áéíóúÁÉÍÓÚñÑ _-]", string.Empty);
            var storedPath = Path.Combine(TaskImportFolder,
                $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}.xlsx");
            await using (var output = System.IO.File.Create(storedPath))
                await file.CopyToAsync(output, cancellationToken);
            await using var input = System.IO.File.OpenRead(storedPath);
            rows = ReadWorksheet(input);
        }
        catch (Exception)
        {
            return BadRequest(new { message = "No fue posible leer este Excel de Acumatica." });
        }

        return await PersistTaskRows(rows, cancellationToken);
    }

    [HttpPost("import-latest-tasks")]
    public async Task<IActionResult> ImportLatestTasks(CancellationToken cancellationToken)
    {
        if (Current is null)
            return Unauthorized(new { message = "Inicia sesión para identificar al propietario de las Tasks." });
        Directory.CreateDirectory(TaskImportFolder);
        var latest = Directory.EnumerateFiles(TaskImportFolder, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
            .Where(path => new FileInfo(path).Length > 1_000)
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
            .ThenByDescending(Path.GetFileName)
            .FirstOrDefault();
        if (latest is null)
            return NotFound(new { message = $"No hay archivos Excel en {TaskImportFolder}" });
        try
        {
            await using var input = System.IO.File.OpenRead(latest);
            var rows = ReadWorksheet(input);
            return await PersistTaskRows(rows, cancellationToken, Path.GetFileName(latest));
        }
        catch (Exception)
        {
            return BadRequest(new { message = "No fue posible leer el Excel más reciente de la carpeta." });
        }
    }

    [HttpGet("task-import-folder")]
    public IActionResult TaskImportFolderInfo()
    {
        if (Current is null) return Unauthorized();
        Directory.CreateDirectory(TaskImportFolder);
        return Ok(new { path = TaskImportFolder });
    }

    private async Task<IActionResult> PersistTaskRows(
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken cancellationToken,
        string? sourceFile = null)
    {
        var acumatica = Current!;
        var userKey = UserKey;
        var expectedOwners = new[]
        {
            acumatica.CurrentDisplayName,
            acumatica.CurrentUsername,
            acumatica.CurrentUsername.Split('@')[0].Replace('.', ' ')
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize).ToHashSet();

        var capturedAt = DateTimeOffset.Now;
        var tasks = rows
            .Where(row => expectedOwners.Contains(Normalize(Field(row, "Propietario"))))
            .Select((row, index) => new TaskSnapshot
            {
                UserKey = userKey,
                ExternalId = $"excel:{Normalize(Field(row, "Resumen"))}:{Field(row, "Fecha de Inicio")}:{index}",
                Summary = Field(row, "Resumen"),
                Status = Field(row, "Estado"),
                CompletionPercent = ParseInteger(Field(row, "(%) Terminado")),
                Category = Field(row, "Categoría"),
                Owner = Field(row, "Propietario"),
                RelatedCase = Field(row, "Entidad Relacionada"),
                StartAt = ParseExcelDate(Field(row, "Fecha de Inicio")),
                DueAt = ParseExcelDate(Field(row, "Fecha Vencimiento")),
                CompletedAt = ParseExcelDate(Field(row, "Completed On")),
                CapturedAt = capturedAt
            }).ToArray();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var previousRun = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        var previousCases = previousRun is null
            ? []
            : await db.Cases.Where(x => x.UserKey == userKey && x.CapturedAt == previousRun.StartedAt).AsNoTracking().ToListAsync(cancellationToken);

        db.Tasks.AddRange(tasks);
        db.Cases.AddRange(previousCases.Select(x => new CaseSnapshot
        {
            UserKey = userKey,
            CaseNumber = x.CaseNumber, Subject = x.Subject, Customer = x.Customer,
            Category = x.Category, Status = x.Status, Reason = x.Reason, Owner = x.Owner,
            CreatedAt = x.CreatedAt, LastIncomingAt = x.LastIncomingAt,
            LastOutgoingAt = x.LastOutgoingAt, CapturedAt = capturedAt
        }));
        db.SyncRuns.Add(new SyncRun
        {
            UserKey = userKey,
            StartedAt = capturedAt, FinishedAt = DateTimeOffset.Now, Succeeded = true,
            CasesRead = previousCases.Count, TasksRead = tasks.Length
        });
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            imported = tasks.Length, totalRows = rows.Count, owner = acumatica.CurrentDisplayName,
            folder = TaskImportFolder, sourceFile
        });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        if (Current is null) return Unauthorized();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lastRun = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastRun is null)
            return Ok(new DashboardSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, null));

        var cases = await db.Cases.Where(x => x.UserKey == userKey && x.CapturedAt == lastRun.StartedAt).ToListAsync(cancellationToken);
        var tasks = await db.Tasks.Where(x => x.UserKey == userKey && x.CapturedAt == lastRun.StartedAt).ToListAsync(cancellationToken);
        var completed = tasks.Count(x => x.CompletionPercent >= 100
            || x.Status.Contains("Completed", StringComparison.OrdinalIgnoreCase)
            || x.Status.Contains("Complet", StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.Now;

        return Ok(new DashboardSummaryDto(
            cases.Count(x => !IsClosed(x.Status)),
            cases.Count(x => x.Status.Contains("Open", StringComparison.OrdinalIgnoreCase)
                || x.Status.Contains("Abierto", StringComparison.OrdinalIgnoreCase)),
            cases.Count(x => x.Status.Contains("Pending", StringComparison.OrdinalIgnoreCase)
                || x.Status.Contains("Pendiente", StringComparison.OrdinalIgnoreCase)),
            cases.Count(x => IsClosed(x.Status)),
            tasks.Count - completed,
            completed,
            tasks.Count(x => x.DueAt < now && x.CompletionPercent < 100),
            tasks.Count == 0 ? 0 : Math.Round(tasks.Average(x => x.CompletionPercent), 1),
            lastRun.FinishedAt));
    }

    [HttpGet("cases")]
    public async Task<IActionResult> Cases(CancellationToken cancellationToken)
    {
        if (Current is null) return Unauthorized();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey).OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        return Ok(run is null ? [] : await db.Cases.Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken));
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(CancellationToken cancellationToken)
    {
        if (Current is null) return Unauthorized();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey).OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        return Ok(run is null ? [] : await db.Tasks.Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt)
            .OrderBy(x => x.DueAt).ToListAsync(cancellationToken));
    }

    [HttpGet("trends")]
    public async Task<ActionResult<IReadOnlyList<TrendPointDto>>> Trends(
        [FromQuery] string period = "week", CancellationToken cancellationToken = default)
    {
        if (Current is null) return Unauthorized();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        if (run is null) return Ok(Array.Empty<TrendPointDto>());

        var cases = await db.Cases.Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt).ToListAsync(cancellationToken);
        var tasks = await db.Tasks.Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt).ToListAsync(cancellationToken);
        var points = new List<TrendPointDto>();
        var today = DateTimeOffset.Now.Date;

        if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
        {
            for (var offset = 5; offset >= 0; offset--)
            {
                var start = new DateTime(today.Year, today.Month, 1).AddMonths(-offset);
                var end = start.AddMonths(1);
                points.Add(new TrendPointDto(start.ToString("MMM"),
                    cases.Count(x => x.CreatedAt >= start && x.CreatedAt < end),
                    tasks.Count(x => x.CompletedAt >= start && x.CompletedAt < end)));
            }
        }
        else
        {
            for (var offset = 6; offset >= 0; offset--)
            {
                var start = today.AddDays(-offset);
                var end = start.AddDays(1);
                points.Add(new TrendPointDto(start.ToString("ddd"),
                    cases.Count(x => x.CreatedAt >= start && x.CreatedAt < end),
                    tasks.Count(x => x.CompletedAt >= start && x.CompletedAt < end)));
            }
        }

        return Ok(points);
    }

    private static bool IsClosed(string status) =>
        status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
        || status.Contains("Cerrado", StringComparison.OrdinalIgnoreCase)
        || status.Contains("Released", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<Dictionary<string, string>> ReadWorksheet(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidDataException("El archivo no contiene la primera hoja.");
        var shared = ReadSharedStrings(archive);
        using var reader = new StreamReader(sheet.Open(), Encoding.UTF8);
        var document = XDocument.Parse(reader.ReadToEnd());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var matrix = document.Descendants(ns + "row").Select(row =>
        {
            var values = new Dictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
                var column = letters.Aggregate(0, (value, letter) => value * 26 + letter - 'A' + 1) - 1;
                var type = cell.Attribute("t")?.Value;
                var value = type == "inlineStr"
                    ? string.Concat(cell.Descendants(ns + "t").Select(x => x.Value))
                    : cell.Element(ns + "v")?.Value ?? string.Empty;
                if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex < shared.Count)
                    value = shared[sharedIndex];
                values[column] = value;
            }
            return values;
        }).ToList();
        if (matrix.Count < 2) return [];

        var headers = matrix[0].ToDictionary(x => x.Key, x => x.Value);
        return matrix.Skip(1).Select(row => headers.ToDictionary(
            header => header.Value,
            header => row.GetValueOrDefault(header.Key, string.Empty),
            StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var document = XDocument.Parse(reader.ReadToEnd());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(x => x.Value))).ToList();
    }

    private static string Field(Dictionary<string, string> row, string requested)
    {
        var match = row.FirstOrDefault(x => Normalize(x.Key) == Normalize(requested));
        return match.Value ?? string.Empty;
    }

    private static string Normalize(string value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var clean = new string(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(clean.Replace('�', 'a').ToLowerInvariant(), @"\s+", " ").Trim();
    }

    private static int ParseInteger(string value) =>
        int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static DateTimeOffset? ParseExcelDate(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial))
            return new DateTimeOffset(DateTime.FromOADate(serial));
        return DateTimeOffset.TryParse(value, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.AssumeLocal, out var date)
            ? date : null;
    }
}
