import { useEffect, useState } from 'react'
import {
  Alert,
  Button,
  Empty,
  Form,
  Input,
  Radio,
  Select,
  Steps,
  message,
} from 'antd'
import { book, errMsg, getAvailability, getDoctors } from './api'
import { computeFreeSlots } from './slots'
import { B } from './labels'
import type { AppointmentDto, DayAvailabilityDto, DoctorDto } from './types'

const STEP = { DOCTOR: 0, DAY: 1, TIME: 2, FORM: 3 }

interface FormValues {
  patientName: string
  patientPhone: string
  nationalCode: string
  insuranceType: string
  notes?: string
}

export default function BookingWizard() {
  const [doctors, setDoctors] = useState<DoctorDto[]>([])
  const [step, setStep] = useState<number>(STEP.DOCTOR)
  const [doctorId, setDoctorId] = useState<string | null>(null)
  const [days, setDays] = useState<DayAvailabilityDto[]>([])
  const [day, setDay] = useState<DayAvailabilityDto | null>(null)
  const [slot, setSlot] = useState<string | null>(null)
  const [booked, setBooked] = useState<AppointmentDto | null>(null)
  const [loadingDays, setLoadingDays] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [form] = Form.useForm<FormValues>()

  useEffect(() => {
    getDoctors()
      .then(setDoctors)
      .catch((error) => message.error(errMsg(error)))
  }, [])

  const doctor = doctors.find((d) => d.id === doctorId)
  const slots = day ? computeFreeSlots(day, doctor ? doctor.defaultVisitMinutes : 30) : []

  async function chooseDoctor(id: string) {
    setDoctorId(id)
    setDay(null)
    setSlot(null)
    setStep(STEP.DAY)
    setLoadingDays(true)
    try {
      const list = await getAvailability(id)
      setDays(list.filter((d) => d.workingHours.length > 0))
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setLoadingDays(false)
    }
  }

  function chooseDay(nextDay: DayAvailabilityDto) {
    setDay(nextDay)
    setSlot(null)
    setStep(STEP.TIME)
  }

  function chooseSlot(time: string) {
    setSlot(time)
    setStep(STEP.FORM)
  }

  function reset() {
    setBooked(null)
    setStep(STEP.DOCTOR)
    setDoctorId(null)
    setDays([])
    setDay(null)
    setSlot(null)
  }

  async function submit(values: FormValues) {
    if (!doctorId || !day || !slot) return
    setSubmitting(true)
    try {
      const dto = await book({
        doctorId,
        patientName: values.patientName.trim(),
        patientPhone: values.patientPhone.trim(),
        nationalCode: values.nationalCode.trim(),
        insuranceType: values.insuranceType,
        startJalali: day.dateJalali + ' ' + slot,
        notes: values.notes ? values.notes.trim() : undefined,
      })
      setBooked(dto)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setSubmitting(false)
    }
  }

  if (booked) {
    return (
      <div className="wizard-card">
        <div className="ticket">
          <div className="ticket-head">
            <b>نوبت‌نامه</b>
            <span>درمانگاه طب الرضا(ع)</span>
          </div>
          <div className="ticket-body">
            <dl style={{ margin: 0 }}>
              <div className="ticket-row">
                <dt>{B.patientName}</dt>
                <dd>{booked.patientName}</dd>
              </div>
              <div className="ticket-row">
                <dt>{B.stepDoctor}</dt>
                <dd>{booked.doctorName}</dd>
              </div>
              <div className="ticket-row">
                <dt>{B.stepTime}</dt>
                <dd>{booked.startJalali}</dd>
              </div>
              <div className="ticket-row">
                <dt>{B.trackingCode}</dt>
                <dd><span className="ticket-code">{booked.shortCode}</span></dd>
              </div>
            </dl>
            <div className="ticket-stamp" aria-hidden="true">
              <b>ثبت شد</b>
              <span>طب الرضا(ع)</span>
            </div>
          </div>
          <hr className="ticket-perf" />
          <div className="ticket-foot">
            <span className="ticket-foot-note">{B.doneSub}</span>
            <Button type="primary" onClick={reset}>{B.newBooking}</Button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="wizard-card">
      <Steps
        size="small"
        current={step}
        items={[
          { title: B.stepDoctor },
          { title: B.stepDay },
          { title: B.stepTime },
          { title: B.stepInfo },
        ]}
      />
      <div style={{ height: 20 }} />

      {step === STEP.DOCTOR && (
        <Select<string>
          style={{ width: '100%' }}
          size="large"
          placeholder={B.chooseDoctor}
          value={doctorId ?? undefined}
          onChange={chooseDoctor}
          options={doctors.map((d) => ({
            value: d.id,
            label: d.fullName + (d.specialty ? ' — ' + d.specialty : ''),
          }))}
        />
      )}

      {step === STEP.DAY && (
        <>
          {!loadingDays && days.length === 0 && <Empty description={B.noWorkingDays} />}
          <div className="day-chip-row">
            {days.map((d) => (
              <button
                key={d.dateJalali}
                className={`day-chip${day?.dateJalali === d.dateJalali ? ' selected' : ''}`}
                onClick={() => chooseDay(d)}
              >
                <span className="day-chip-weekday">{d.weekdayFa}</span>
                <span className="day-chip-date" dir="ltr">{d.dateJalali}</span>
              </button>
            ))}
          </div>
        </>
      )}

      {step === STEP.TIME && day && (
        <>
          {slots.length === 0 && <Empty description={B.noFreeTime} />}
          <div className="slot-grid">
            {slots.map((s) => (
              <button
                key={s}
                className={`slot-btn${slot === s ? ' selected' : ''}`}
                onClick={() => chooseSlot(s)}
              >
                {s}
              </button>
            ))}
          </div>
        </>
      )}

      {step === STEP.FORM && day && slot && (
        <Form form={form} layout="vertical" onFinish={submit} className="patient-form">
          <Alert
            type="info"
            showIcon
            message={`${day.weekdayFa} ${day.dateJalali} ساعت ${slot}`}
            style={{ marginBottom: 16 }}
          />
          <Form.Item<FormValues>
            name="patientName"
            label={B.patientName}
            rules={[{ required: true, message: 'نام لازم است' }]}
          >
            <Input size="large" />
          </Form.Item>
          <Form.Item<FormValues>
            name="patientPhone"
            label={B.patientPhone}
            rules={[
              { required: true, message: 'موبایل لازم است' },
              { pattern: /^(\+?98|0)?9\d{9}$|^0\d{9,10}$|^\+?\d{7,15}$/, message: 'شماره معتبر نیست' },
            ]}
          >
            <Input size="large" placeholder="09xxxxxxxxx" />
          </Form.Item>
          <Form.Item<FormValues>
            name="nationalCode"
            label="کد ملی"
            rules={[
              { required: true, message: 'کد ملی لازم است' },
              { pattern: /^\d{10}$/, message: 'کد ملی باید ۱۰ رقم باشد' },
            ]}
          >
            <Input size="large" placeholder="0123456789" maxLength={10} dir="ltr" />
          </Form.Item>
          <Form.Item<FormValues>
            name="insuranceType"
            label="نوع بیمه"
            rules={[{ required: true, message: 'انتخاب بیمه لازم است' }]}
          >
            <Radio.Group
              options={[
                { value: 'Basic', label: 'بیمه پایه' },
                { value: 'Supplementary', label: 'بیمه تکمیلی' },
              ]}
            />
          </Form.Item>
          <Form.Item<FormValues> name="notes" label={B.notes}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Button type="primary" htmlType="submit" block size="large" loading={submitting}>
            {B.submit}
          </Button>
        </Form>
      )}
    </div>
  )
}
