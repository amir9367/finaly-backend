import { useEffect, useState } from 'react'
import { Empty, Spin, message } from 'antd'
import { getDoctors, getAvailability, errMsg } from '../api'
import type { DoctorDto, DayAvailabilityDto } from '../types'

const LOCATION_ORDER = ['طبقه اول', 'طبقه دوم', 'زیرزمین']

function avatarLetter(name: string): string {
  const stripped = name
    .replace(/^دکتر\s+/, '')
    .replace(/^آقای\s+/, '')
    .replace(/^خانم\s+/, '')
    .trim()
  return stripped[0] ?? name[0] ?? '؟'
}

interface DoctorCardProps {
  doctor: DoctorDto
  isOpen: boolean
  onToggle: () => void
}

function DoctorCard({ doctor, isOpen, onToggle }: DoctorCardProps) {
  const [days, setDays] = useState<DayAvailabilityDto[] | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleToggle() {
    if (!isOpen && days === null) {
      setLoading(true)
      try {
        const list = await getAvailability(doctor.id)
        setDays(list.filter(d => d.workingHours.length > 0))
      } catch (e) {
        message.error(errMsg(e))
      } finally {
        setLoading(false)
      }
    }
    onToggle()
  }

  return (
    <div className="doctor-card">
      <div className="doctor-card-header" onClick={handleToggle}>
        <div className="doctor-avatar">{avatarLetter(doctor.fullName)}</div>
        <div className="doctor-info">
          <div className="doctor-name">{doctor.fullName}</div>
          <div className="doctor-specialty">{doctor.specialty}</div>
          {doctor.location && (
            <div className="doctor-location">{doctor.location}</div>
          )}
        </div>
        <div className="doctor-toggle">{isOpen ? '▲' : '▼'}</div>
      </div>

      {isOpen && (
        <div className="doctor-schedule">
          {loading && <Spin size="small" />}
          {!loading && days !== null && days.length === 0 && (
            <p className="no-schedule">برنامه‌ای برای دو هفته آینده ثبت نشده است.</p>
          )}
          {!loading && days && days.length > 0 && (
            <div className="schedule-list">
              {days.map(d => (
                <div className="schedule-item" key={d.dateJalali}>
                  <span className="schedule-day">{d.weekdayFa} {d.dateJalali}</span>
                  <div className="schedule-hours">
                    {d.workingHours.map((w, i) => (
                      <span className="schedule-hour" key={i}>{w.from} – {w.to}</span>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

export default function DoctorsPage() {
  const [doctors, setDoctors] = useState<DoctorDto[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [openId, setOpenId] = useState<string | null>(null)

  useEffect(() => {
    getDoctors()
      .then(setDoctors)
      .catch(e => message.error(errMsg(e)))
      .finally(() => setLoading(false))
  }, [])

  const filtered = doctors.filter(d =>
    d.fullName.includes(search) || d.specialty.includes(search) || d.location.includes(search)
  )

  // Group by location in defined order
  const groups: { loc: string; docs: DoctorDto[] }[] = []
  const grouped: Record<string, DoctorDto[]> = {}
  filtered.forEach(d => {
    const loc = d.location || 'سایر'
    if (!grouped[loc]) grouped[loc] = []
    grouped[loc].push(d)
  })
  // Sort: طبقه اول → طبقه دوم → زیرزمین → anything else
  const orderedKeys = [
    ...LOCATION_ORDER.filter(k => grouped[k]),
    ...Object.keys(grouped).filter(k => !LOCATION_ORDER.includes(k)),
  ]
  orderedKeys.forEach(loc => groups.push({ loc, docs: grouped[loc] }))

  function handleToggle(id: string) {
    setOpenId(prev => (prev === id ? null : id))
  }

  return (
    <>
      <header className="hero hero-sm">
        <h1>پزشکان درمانگاه</h1>
        <p>لیست پزشکان متخصص و برنامه حضور آن‌ها</p>
      </header>

      <main className="page-main">
        <div className="content-wrap">
          <div className="search-bar">
            <input
              className="search-input"
              type="text"
              placeholder="جستجو در نام پزشک، تخصص یا محل..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>

          {loading && <div style={{ textAlign: 'center', padding: 40 }}><Spin /></div>}
          {!loading && filtered.length === 0 && <Empty description="پزشکی یافت نشد" style={{ marginTop: 40 }} />}

          {!loading && groups.map(({ loc, docs }) => (
            <div key={loc} className="location-group">
              <div className="location-group-title">{loc}</div>
              <div className="doctors-grid">
                {docs.map(d => (
                  <DoctorCard
                    key={d.id}
                    doctor={d}
                    isOpen={openId === d.id}
                    onToggle={() => handleToggle(d.id)}
                  />
                ))}
              </div>
            </div>
          ))}
        </div>
      </main>
    </>
  )
}
