import { useState } from 'react'
import AppointmentPage from './pages/AppointmentPage'
import DoctorsPage from './pages/DoctorsPage'
import AboutPage from './pages/AboutPage'
import HomePage from './pages/HomePage'

type Page = 'home' | 'appointment' | 'doctors' | 'about'

const CLINIC = {
  name: 'طب الرضا(ع)',
  tagline: 'درمانگاه شبانه‌روزی',
  phone: '۳۴۲۴۶',
  phoneHref: 'tel:34246',
}

export default function App() {
  const [page, setPage] = useState<Page>('home')

  return (
    <div className="page-shell">
      <nav className="clinic-nav">
        <button className="nav-brand-btn" onClick={() => setPage('home')}>
          <img src="/logo.jpg" alt="طب الرضا" className="nav-logo" />
          <div className="clinic-brand-text">
            <b>{CLINIC.name}</b>
            <span>{CLINIC.tagline}</span>
          </div>
        </button>
        <div className="nav-links">
          <button className={`nav-link${page === 'home' ? ' active' : ''}`} onClick={() => setPage('home')}>خانه</button>
          <button className={`nav-link${page === 'appointment' ? ' active' : ''}`} onClick={() => setPage('appointment')}>رزرو نوبت</button>
          <button className={`nav-link${page === 'doctors' ? ' active' : ''}`} onClick={() => setPage('doctors')}>پزشکان</button>
          <button className={`nav-link${page === 'about' ? ' active' : ''}`} onClick={() => setPage('about')}>درباره ما</button>
        </div>
        <a className="nav-phone" href={CLINIC.phoneHref}>📞 {CLINIC.phone}</a>
      </nav>

      {page === 'home' && (
        <HomePage
          onBook={() => setPage('appointment')}
          onDoctors={() => setPage('doctors')}
          onAbout={() => setPage('about')}
        />
      )}
      {page === 'appointment' && <AppointmentPage />}
      {page === 'doctors' && <DoctorsPage />}
      {page === 'about' && <AboutPage />}

      <footer className="clinic-footer">
        <img src="/logo.jpg" alt="طب الرضا" className="footer-logo" />
        <b>درمانگاه شبانه‌روزی {CLINIC.name}</b>
        <p>تهران، پیروزی — بلوار ابوذر، پل دوم — خیابان ائمه اطهار، نبش برادران باقری، پلاک ۲۳</p>
        <a className="footer-phone" href={CLINIC.phoneHref}>وقت‌دهی: 📞 {CLINIC.phone}</a>
        <p className="copyright">تمامی حقوق برای درمانگاه {CLINIC.name} محفوظ است.</p>
      </footer>
    </div>
  )
}
