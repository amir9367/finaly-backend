namespace Clinic.Api.Domain;

public enum AppointmentStatus
{
    Booked = 0,
    CancelledByPatient = 1,
    CancelledByClinic = 2,
}

public enum AppointmentSource
{
    Online = 0,
    Admin = 1,
    ExcelImport = 2,
}

public enum SmsType
{
    BookingConfirmation = 0,
    CancellationNotice = 1,
    CancelOtp = 2,
}

public enum SmsStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}

public enum InsuranceType
{
    Basic = 0,        // بیمه پایه
    Supplementary = 1 // بیمه تکمیلی
}
