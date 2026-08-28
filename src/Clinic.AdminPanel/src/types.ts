export interface ScheduleDto {
  weekday: number
  startTime: string
  endTime: string
  isActive: boolean
}

export interface DoctorAdminDto {
  id: string
  fullName: string
  specialty: string
  location: string
  defaultVisitMinutes: number
  isActive: boolean
  createdAt: string
  schedules: ScheduleDto[]
}

export interface AppointmentDto {
  id: string
  shortCode: string
  doctorId: string
  doctorName: string
  patientName: string
  patientPhone: string
  nationalCode?: string | null
  insuranceType?: string | null
  startJalali: string
  endJalali: string
  startsAtUtc: string
  endsAtUtc: string
  status: 'Booked' | 'CancelledByPatient' | 'CancelledByClinic' | string
  source: 'Online' | 'Admin' | 'ExcelImport' | string
  notes?: string | null
  cancelReason?: string | null
}

export interface SmsLogDto {
  id: string
  appointmentId?: string | null
  phone: string
  type: 'BookingConfirmation' | 'CancellationNotice' | 'CancelOtp' | string
  body: string
  status: 'Pending' | 'Sent' | 'Failed' | string
  providerMessageId?: string | null
  error?: string | null
  createdAt: string
  sentAt?: string | null
}

export interface DashboardStats {
  todayAppointments: number
  next7DaysAppointments: number
  activeDoctors: number
  failedSms24h: number
}

export interface ImportRowError {
  row: number
  error: string
}

export interface ImportResult {
  totalRows: number
  imported: number
  updated: number
  skipped: number
  errors: ImportRowError[]
  syncLogId: string
}

export const WEEKDAY_FA = ['شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه']

export const STATUS_FA: Record<string, string> = {
  Booked: 'رزرو شده',
  CancelledByPatient: 'لغو توسط بیمار',
  CancelledByClinic: 'لغو توسط کلینیک',
}

export const SOURCE_FA: Record<string, string> = {
  Online: 'آنلاین',
  Admin: 'پنل',
  ExcelImport: 'اکسل',
}

export const SMS_TYPE_FA: Record<string, string> = {
  BookingConfirmation: 'تایید نوبت',
  CancellationNotice: 'اعلام لغو',
  CancelOtp: 'کد لغو',
}
