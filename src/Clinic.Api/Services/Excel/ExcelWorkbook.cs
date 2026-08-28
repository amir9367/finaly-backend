using Clinic.Api.Common;
using Clinic.Api.Domain;
using ClosedXML.Excel;

namespace Clinic.Api.Services.Excel;

public sealed record ParsedRow
{
    public required int RowNumber { get; init; }
    public required string DoctorName { get; init; }
    public required string Specialty { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Start { get; init; }
    public int? DurationMinutes { get; init; }
    public required string PatientName { get; init; }
    public required string Phone { get; init; }
    public required string NationalCode { get; init; }
    public required InsuranceType InsuranceType { get; init; }
    public AppointmentStatus Status { get; init; } = AppointmentStatus.Booked;
    public string? Notes { get; init; }
}

public sealed record RowProblem(int RowNumber, string Message);

public sealed record ParseOutcome(List<ParsedRow> Rows, List<RowProblem> Problems);

public sealed record ExportRow(
    string DoctorName,
    string Specialty,
    DateTime StartsAtUtc,
    int DurationMinutes,
    string PatientName,
    string Phone,
    string NationalCode,
    InsuranceType? InsuranceType,
    AppointmentStatus Status,
    string? Notes);

/// <summary>
/// Builds and reads the clinic Excel template (ClosedXML).
/// Dates are Jalali text ("1405/06/04"), times 24h text ("14:30") — see ReadMe sheet.
/// </summary>
public static class ExcelWorkbook
{
    public const string SheetAppointments = "Appointments";
    public const string SheetReadMe = "ReadMe";

    /// <summary>Hard cap on data rows per import — bounds parse memory and DB work.</summary>
    public const int MaxDataRows = 5_000;

    public static readonly string[] Headers =
    [
        "Doctor", "Specialty", "Date", "Start", "Duration(min)",
        "Patient Name", "Patient Phone", "National Code", "Insurance", "Status", "Notes",
    ];

    public static byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.Worksheets.Add(SheetAppointments);
        WriteHeaders(sheet);
        AddExampleRows(sheet);
        sheet.SheetView.FreezeRows(1);

        var readme = workbook.Worksheets.Add(SheetReadMe);
        var instructions = new[]
        {
            "CLINIC APPOINTMENTS TEMPLATE / قالب نوبت‌های کلینیک",
            "",
            "EN:",
            "- One appointment per row on the 'Appointments' sheet.",
            "- Doctor must exactly match a doctor created in the admin panel (case-insensitive).",
            "- Date is Jalali text like 1405/06/04 (you can also use - as separator).",
            "- Start is 24-hour text like 14:30.",
            "- Duration(min) is optional; when empty the doctor's default visit length is used.",
            "- Status is one of: Booked, CancelledByPatient, CancelledByClinic ('Cancelled' alone means cancelled by the clinic; empty means Booked).",
            "- Do not rename the sheets or reorder columns.",
            "",
            "FA:",
            "- هر ردیف یک نوبت است (شیت Appointments).",
            "- نام پزشک باید دقیقاً با نام ثبت‌شده در پنل مطابقت داشته باشد.",
            "- تاریخ به صورت شمسی و متنی مانند 1405/06/04 وارد شود.",
            "- ساعت به صورت متنی و ۲۴ ساعته مانند 14:30 وارد شود.",
            "- مدت ویزیت اختیاری است؛ در صورت خالی بودن، مدت پیش‌فرض پزشک استفاده می‌شود.",
            "- وضعیت یکی از: Booked یا CancelledByPatient یا CancelledByClinic است («Cancelled» یعنی لغو توسط کلینیک؛ خالی یعنی Booked).",
            "- نام شیت‌ها یا ترتیب ستون‌ها را تغییر ندهید.",
        };
        for (var i = 0; i < instructions.Length; i++)
            readme.Cell(i + 1, 1).Value = instructions[i];
        readme.Column(1).Width = 90;
        readme.Cell(1, 1).Style.Font.SetBold();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Export for admin "دانلود اکسل" — exactly the 5 patient fields requested, sorted chronologically
    public static readonly string[] ExportHeaders =
    [
        "نام بیمار", "کد ملی", "شماره تلفن", "تاریخ و ساعت", "نوع بیمه"
    ];

    public static byte[] Export(IEnumerable<ExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetAppointments);
        // Write 5-column export headers (not the 11-column template headers)
        for (var i = 0; i < ExportHeaders.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = ExportHeaders[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#79E19B");
            cell.Style.Font.FontColor = XLColor.FromHtml("#171717");
        }

        var r = 2;
        foreach (var row in rows.OrderBy(x => x.StartsAtUtc))
        {
            var local = JalaliDate.UtcToTehran(row.StartsAtUtc);
            var jalaliDateTime = $"{CalendarYear(local):0000}/{CalendarMonth(local):00}/{CalendarDay(local):00} {local.Hour:00}:{local.Minute:00}";
            var insuranceFa = row.InsuranceType switch
            {
                InsuranceType.Basic => "پایه",
                InsuranceType.Supplementary => "تکمیلی",
                _ => ""
            };
            sheet.Cell(r, 1).Value = SafeText(row.PatientName);
            sheet.Cell(r, 2).Value = row.NationalCode;
            sheet.Cell(r, 3).Value = row.Phone;
            sheet.Cell(r, 4).Value = jalaliDateTime;
            sheet.Cell(r, 5).Value = insuranceFa;
            r++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Parses an uploaded template. Format problems are returned per-row, never thrown per-row.</summary>
    public static ParseOutcome Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        // TryGetWorksheet is a bool + out API — calling it as if it returned a
        // nullable worksheet never compiled.
        if (!workbook.TryGetWorksheet(SheetAppointments, out var sheet))
            throw new ValidationException($"Worksheet '{SheetAppointments}' was not found in the uploaded file.");

        var rows = new List<ParsedRow>();
        var problems = new List<RowProblem>();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaxDataRows)
            throw new ValidationException(
                $"The file has more than {MaxDataRows} data rows — split it into smaller files.");

        // Detect new 11-column template vs old 9-column (backwards compat)
        var header8 = sheet.Cell(1, 8).GetString().Trim();
        var isNewFormat = header8.Equals("National Code", StringComparison.OrdinalIgnoreCase);

        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            string Cell(int index) => row.Cell(index).GetString().Trim();

            string doctorName, specialty, dateText, startText, durationText, patientName, phoneText, nationalCodeText, insuranceText, statusText, notes;
            if (isNewFormat)
            {
                doctorName = Cell(1);
                specialty = Cell(2);
                dateText = Cell(3);
                startText = Cell(4);
                durationText = Cell(5);
                patientName = Cell(6);
                phoneText = Cell(7);
                nationalCodeText = Cell(8);
                insuranceText = Cell(9);
                statusText = Cell(10);
                notes = Cell(11);
            }
            else
            {
                // Old template fallback (9 cols) — NationalCode/Insurance empty
                doctorName = Cell(1);
                specialty = Cell(2);
                dateText = Cell(3);
                startText = Cell(4);
                durationText = Cell(5);
                patientName = Cell(6);
                phoneText = Cell(7);
                nationalCodeText = "";
                insuranceText = "";
                statusText = Cell(8);
                notes = Cell(9);
            }

            if (doctorName.Length == 0 && dateText.Length == 0 && patientName.Length == 0 && phoneText.Length == 0)
                continue; // blank row

            var errors = new List<string>();
            if (doctorName.Length == 0) errors.Add("Doctor is empty");

            if (!JalaliDate.TryParse(dateText, out var tehranLocal))
                errors.Add($"Invalid Date '{dateText}' — expected Jalali text like 1405/06/04");

            if (!JalaliDate.TryParseTime(startText, out var hour, out var minute))
                errors.Add($"Invalid Start '{startText}' — expected 24-hour text like 14:30");

            int? duration = null;
            if (durationText.Length > 0)
            {
                if (!int.TryParse(durationText, out var parsedDuration) || parsedDuration < 5 || parsedDuration > 480)
                    errors.Add($"Duration(min) '{durationText}' must be a number between 5 and 480");
                else
                    duration = parsedDuration;
            }

            if (patientName.Length == 0) errors.Add("Patient Name is empty");
            if (patientName.Length > 120) errors.Add("Patient Name exceeds 120 characters");
            if (doctorName.Length > 150) errors.Add("Doctor exceeds 150 characters");
            if (specialty.Length > 150) errors.Add("Specialty exceeds 150 characters");
            if (notes.Length > 500) errors.Add("Notes exceed 500 characters");

            var phone = PhoneNormalizer.Normalize(phoneText);
            if (phone.Length < 7 || phone.Length > 16)
                errors.Add($"Patient Phone '{phoneText}' looks invalid");

            // National Code — required in new format, optional for old compat
            var nationalCode = nationalCodeText.Trim();
            if (isNewFormat)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(nationalCode, @"^\d{10}$"))
                    errors.Add($"National Code '{nationalCodeText}' must be 10 digits");
            }
            else if (nationalCode.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(nationalCode, @"^\d{10}$"))
            {
                errors.Add($"National Code '{nationalCodeText}' must be 10 digits");
            }

            // Insurance — required in new format
            InsuranceType insuranceType = InsuranceType.Basic;
            if (isNewFormat)
            {
                if (!TryParseInsurance(insuranceText, out insuranceType))
                    errors.Add($"Insurance '{insuranceText}' must be 'پایه' or 'تکمیلی' (or Basic/Supplementary)");
            }
            else if (insuranceText.Length > 0 && !TryParseInsurance(insuranceText, out insuranceType))
            {
                errors.Add($"Insurance '{insuranceText}' must be 'پایه' or 'تکمیلی'");
            }

            var status = AppointmentStatus.Booked;
            if (!TryParseStatus(statusText, out status))
            {
                errors.Add(
                    $"Unknown Status '{statusText}' — use Booked, CancelledByPatient or CancelledByClinic");
            }

            if (errors.Count > 0)
            {
                problems.AddRange(errors.Select(e => new RowProblem(r, e)));
                continue;
            }

            rows.Add(new ParsedRow
            {
                RowNumber = r,
                DoctorName = doctorName,
                Specialty = specialty,
                Date = DateOnly.FromDateTime(tehranLocal),
                Start = new TimeOnly(hour, minute),
                DurationMinutes = duration,
                PatientName = patientName,
                Phone = phone,
                NationalCode = nationalCode.Length > 0 ? nationalCode : "0000000000",
                InsuranceType = insuranceType,
                Status = status,
                Notes = notes.Length > 0 ? notes : null,
            });
        }

        return new ParseOutcome(rows, problems);
    }

    /// <summary>
    /// Explicit status parsing. <see cref="Enum.TryParse{T}"/> alone would
    /// happily accept numeric junk ("7"), producing out-of-range enum values
    /// that slip past the status=0 double-booking constraint.
    /// </summary>
    internal static bool TryParseStatus(string text, out AppointmentStatus status)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "":
            case "booked":
                status = AppointmentStatus.Booked;
                return true;
            case "cancelledbypatient":
            case "cancelled by patient":
                status = AppointmentStatus.CancelledByPatient;
                return true;
            case "cancelledbyclinic":
            case "cancelled by clinic":
            case "cancelled": // template shorthand — staff cancel, not patients
                status = AppointmentStatus.CancelledByClinic;
                return true;
            default:
                status = AppointmentStatus.Booked;
                return false;
        }
    }

    internal static bool TryParseInsurance(string text, out InsuranceType insurance)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "":
            case "پایه":
            case "پايه":
            case "basic":
            case "bimeh payeh":
            case "bimepayeh":
                insurance = InsuranceType.Basic;
                return true;
            case "تکمیلی":
            case "تکميل": 
            case "supplementary":
            case "takmili":
            case "bimeh takmili":
                insurance = InsuranceType.Supplementary;
                return true;
            default:
                insurance = InsuranceType.Basic;
                return false;
        }
    }

    /// <summary>Prefixes values Excel could otherwise interpret as a formula.</summary>
    internal static string SafeText(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
            ? " " + value
            : value;

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }
    }

    private static void AddExampleRows(IXLWorksheet sheet)
    {
        // Greyed example so staff can see the expected shape at a glance.
        sheet.Cell(2, 1).Value = "دکتر علی رضایی (example)";
        sheet.Cell(2, 3).Value = "1405/06/04";
        sheet.Cell(2, 4).Value = "09:00";
        sheet.Cell(2, 6).Value = "نام بیمار (example)";
        sheet.Cell(2, 7).Value = "09121234567";
        sheet.Cell(2, 8).Value = "0123456789";
        sheet.Cell(2, 9).Value = "پایه";
        sheet.Cell(2, 10).Value = "Booked";
        sheet.Cell(2, 11).Value = "This example row must be deleted before use.";
        sheet.Row(2).Style.Font.SetFontColor(XLColor.Gray);
        sheet.Row(2).Style.Font.SetItalic();
    }

    private static readonly System.Globalization.PersianCalendar ExportCalendar = new();

    private static int CalendarYear(DateTime tehranLocal) => ExportCalendar.GetYear(tehranLocal);
    private static int CalendarMonth(DateTime tehranLocal) => ExportCalendar.GetMonth(tehranLocal);
    private static int CalendarDay(DateTime tehranLocal) => ExportCalendar.GetDayOfMonth(tehranLocal);
}
