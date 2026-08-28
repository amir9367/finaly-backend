using Clinic.Api.Common;
using Clinic.Api.Dtos;
using Clinic.Api.Services.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace Clinic.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/excel")]
public class ExcelController(IExcelSyncService sync) : ControllerBase
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int MaxUploadBytes = 5_000_000;      // compressed upload
    private const int MaxDecompressedBytes = 50_000_000; // zip entries total
    private const int MaxZipEntryBytes = 20_000_000;     // any single entry

    /// <summary>Imports an uploaded template. Bad rows are reported, never silently dropped.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<ImportResultDto>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("Attach an .xlsx file in the 'file' field.");
        if (file.Length > MaxUploadBytes)
            throw new ValidationException("The workbook is too large (max 5 MB).");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Only .xlsx files are supported.");

        // Buffer once: the size checks below need seeking and re-reading anyway.
        using var buffered = new MemoryStream();
        await file.CopyToAsync(buffered, ct);

        // An .xlsx is a zip archive — anything else would die later inside
        // ClosedXML as an opaque 500. Reject it here with a clean 400.
        // (Plain array + explicit length check: stackalloc is illegal inside
        // async methods, and ReadExactly on a shorter file throws.)
        buffered.Position = 0;
        var header = new byte[4];
        if (buffered.Length < header.Length || buffered.Read(header, 0, header.Length) != header.Length
            || !header.AsSpan().SequenceEqual("PK\x03\x04"u8))
            throw new ValidationException("This file is not a valid .xlsx workbook.");

        // Zip-bomb guard: a small .xlsx can decompress to gigabytes of XML and
        // exhaust memory when ClosedXML loads it. Read declared entry sizes
        // from the central directory before parsing.
        buffered.Position = 0;
        using (var archive = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: true))
        {
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxZipEntryBytes)
                    throw new ValidationException("The workbook's decompressed content is too large.");
                total += entry.Length;
            }
            if (total > MaxDecompressedBytes)
                throw new ValidationException("The workbook's decompressed content is too large.");
        }

        buffered.Position = 0;
        var result = await sync.ImportAsync(buffered, file.FileName, ct);
        return Ok(result);
    }

    /// <summary>Downloads the empty appointment template.</summary>
    [HttpGet("template")]
    public IActionResult Template() =>
        File(sync.TemplateBytes(), XlsxContentType, "clinic-appointments-template.xlsx");

    /// <summary>Exports current data (filtered by same params as appointments list) as the same template.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] Guid? doctorId, [FromQuery] string? status, CancellationToken ct)
    {
        var bytes = await sync.ExportFilteredAsync(doctorId, status, ct);
        var name = $"clinic-appointments-{JalaliDate.ToJalaliDate(DateTime.UtcNow)}.xlsx";
        return File(bytes, XlsxContentType, name);
    }
}
