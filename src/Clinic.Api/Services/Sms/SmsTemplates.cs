using Clinic.Api.Domain;

namespace Clinic.Api.Services.Sms;

/// <summary>Persian message templates. Keep texts short (SMS 70-char GSM7 limit per segment).</summary>
public static class SmsTemplates
{
    public static string BookingConfirmation(string patientName, string doctorName, string startJalali, string shortCode, string? location = null)
    {
        var loc = string.IsNullOrWhiteSpace(location) ? "" : $" — محل: {location}";
        return $"{patientName} عزیز، نوبت شما با {doctorName} در تاریخ {startJalali}{loc} ثبت شد. کد پیگیری: {shortCode}";
    }

    public static string CancellationNotice(string patientName, string doctorName, string startJalali) =>
        $"{patientName} عزیز، نوبت شما با دکتر {doctorName} در تاریخ {startJalali} لغو شد. برای تعیین نوبت جدید با کلینیک تماس بگیرید.";

    public static string CancelOtp(string code) =>
        $"کد تایید لغو نوبت شما: {code}";

    public static string BodyFor(SmsType type, string patientName, string doctorName, string startJalali, string? extra = null) =>
        type switch
        {
            SmsType.BookingConfirmation => BookingConfirmation(patientName, doctorName, startJalali, extra ?? ""),
            SmsType.CancellationNotice => CancellationNotice(patientName, doctorName, startJalali),
            SmsType.CancelOtp => CancelOtp(extra ?? ""),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}
