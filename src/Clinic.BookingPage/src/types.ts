export interface DoctorDto {
  id: string
  fullName: string
  specialty: string
  location: string
  defaultVisitMinutes: number
}

export interface Interval {
  from: string // HH:mm
  to: string   // HH:mm
}

export interface DayAvailabilityDto {
  dateJalali: string   // 1405/06/04
  dateIso: string      // 2026-08-26
  weekdayFa: string
  workingHours: Interval[]
  busy: Interval[]
}

export interface AppointmentDto {
  id: string
  shortCode: string
  doctorName: string
  patientName: string
  patientPhone: string
  nationalCode?: string | null
  insuranceType?: string | null
  startJalali: string
  endJalali: string
  status: string
}
