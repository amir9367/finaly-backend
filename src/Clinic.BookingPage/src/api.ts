import axios from 'axios'
import type { AppointmentDto, DayAvailabilityDto, DoctorDto } from './types'

const http = axios.create({ baseURL: '/api', timeout: 30_000 })

export function errMsg(e: unknown): string {
  const anyErr = e as { response?: { data?: { error?: string } } }
  return anyErr?.response?.data?.error ?? (e as Error)?.message ?? 'خطای ناشناخته'
}

export async function getDoctors(): Promise<DoctorDto[]> {
  const response = await http.get<DoctorDto[]>('/doctors')
  return response.data
}

export async function getAvailability(doctorId: string): Promise<DayAvailabilityDto[]> {
  const response = await http.get<DayAvailabilityDto[]>(`/doctors/${doctorId}/availability`)
  return response.data
}

export async function book(payload: {
  doctorId: string
  patientName: string
  patientPhone: string
  nationalCode: string
  insuranceType: string
  startJalali: string
  notes?: string
}): Promise<AppointmentDto> {
  const response = await http.post<AppointmentDto>('/appointments', payload)
  return response.data
}

// POST (not GET) keeps the patient's phone out of URLs → out of access logs.
export async function lookupByCode(shortCode: string, phone: string): Promise<AppointmentDto> {
  const response = await http.post<AppointmentDto>('/appointments/lookup', {
    shortCode,
    phone,
  })
  return response.data
}

export async function requestCancelOtp(appointmentId: string, phone: string): Promise<void> {
  await http.post(`/appointments/${appointmentId}/cancel/request`, { phone })
}

export async function confirmCancel(appointmentId: string, phone: string, code: string): Promise<AppointmentDto> {
  const response = await http.post<AppointmentDto>(`/appointments/${appointmentId}/cancel/confirm`, {
    phone,
    code,
  })
  return response.data
}
