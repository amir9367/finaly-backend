import { B } from '../labels'

interface Props {
  onBook: () => void
  onDoctors: () => void
  onAbout: () => void
}

const SERVICES = [
  { icon: '🩺', title: 'ویزیت عمومی', desc: 'پزشک عمومی ۲۴ ساعته' },
  { icon: '🦷', title: 'دندانپزشکی', desc: 'خدمات دندانپزشکی کامل' },
  { icon: '🔬', title: 'آزمایشگاه', desc: 'آزمایش‌های تخصصی' },
  { icon: '📡', title: 'رادیولوژی', desc: 'تصویربرداری پیشرفته' },
  { icon: '💊', title: 'داروخانه', desc: 'داروخانه شبانه‌روزی' },
  { icon: '🏥', title: 'اورژانس', desc: 'خدمات اورژانسی فوری' },
]

const STATS = [
  { num: '+۲۰', label: 'تخصص پزشکی' },
  { num: '۲۴', label: 'ساعت فعال' },
  { num: '+۱۲', label: 'بیمه طرف قرارداد' },
  { num: '+۱۵', label: 'سال سابقه' },
]

const STEPS = [
  { n: '۱', title: 'انتخاب پزشک', desc: 'پزشک یا تخصص مورد نظر خود را انتخاب کنید.' },
  { n: '۲', title: 'انتخاب روز و ساعت', desc: 'از بین ساعات آزاد، وقت مناسب خود را رزرو کنید.' },
  { n: '۳', title: 'دریافت تأییدیه', desc: 'کد پیگیری و پیامک تأیید فوری دریافت کنید.' },
]

export default function HomePage({ onBook, onDoctors, onAbout }: Props) {
  return (
    <div className="home-page">
      <section className="home-hero">
        <div className="home-hero-inner">
          <img src="/logo.jpg" alt="طب الرضا" className="home-logo" />
          <div className="home-hero-text">
            <div className="hero-badge">شبانه‌روزی · ۲۴ ساعته · از ۱۳۸۵</div>
            <h1>
              درمانگاه <span>طب الرضا(ع)</span>
            </h1>
            <p>
              خدمات درمانی تخصصی در بیش از ۲۰ رشته پزشکی — نوبت‌دهی آنلاین بدون
              صف انتظار، هر روز هفته.
            </p>
            <div className="home-hero-btns">
              <button className="btn-primary" onClick={onBook}>
                رزرو نوبت آنلاین
              </button>
              <button className="btn-outline" onClick={onDoctors}>
                مشاهده پزشکان
              </button>
            </div>
          </div>
        </div>
      </section>

      <section className="home-stats">
        {STATS.map((s) => (
          <div key={s.label} className="stat-item">
            <span className="stat-num">{s.num}</span>
            <span className="stat-label">{s.label}</span>
          </div>
        ))}
      </section>

      <section className="home-section">
        <div className="content-wrap">
          <div className="section-title">
            <h2>خدمات درمانگاه</h2>
            <p>زیر یک سقف — از ویزیت تا آزمایش و دارو</p>
          </div>
          <div className="services-grid">
            {SERVICES.map((sv) => (
              <div key={sv.title} className="service-card">
                <span className="service-icon">{sv.icon}</span>
                <h4>{sv.title}</h4>
                <p>{sv.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="home-how">
        <div className="content-wrap">
          <div className="section-title">
            <h2>چطور نوبت بگیرم؟</h2>
            <p>در کمتر از یک دقیقه</p>
          </div>
          <div className="steps-row">
            {STEPS.map((st) => (
              <div key={st.n} className="step-card">
                <div className="step-num">{st.n}</div>
                <h4>{st.title}</h4>
                <p>{st.desc}</p>
              </div>
            ))}
          </div>
          <div style={{ textAlign: 'center', marginTop: 32 }}>
            <button className="btn-primary" onClick={onBook}>
              همین الان رزرو کنید
            </button>
          </div>
        </div>
      </section>

      <section className="home-info-strip">
        <div className="content-wrap home-info-grid">
          <div className="info-strip-item">
            <span className="info-strip-icon">📍</span>
            <div>
              <b>آدرس</b>
              <p>تهران، پیروزی — بلوار ابوذر، پل دوم — خیابان ائمه اطهار، نبش برادران باقری، پلاک ۲۳</p>
            </div>
          </div>
          <div className="info-strip-item">
            <span className="info-strip-icon">📞</span>
            <div>
              <b>وقت‌دهی تلفنی</b>
              <p>
                <a href="tel:34246" className="info-strip-phone">۳۴۲۴۶</a>
              </p>
            </div>
          </div>
          <div className="info-strip-item">
            <span className="info-strip-icon">🕐</span>
            <div>
              <b>ساعات کاری</b>
              <p>تمام روزهای هفته، ۲۴ ساعته</p>
            </div>
          </div>
        </div>
      </section>

      <section className="home-cta">
        <div className="home-cta-inner">
          <h2>آماده رزرو نوبت هستید؟</h2>
          <p>سریع، آسان و بدون نیاز به تماس تلفنی</p>
          <div className="home-hero-btns">
            <button className="btn-primary btn-lg" onClick={onBook}>
              رزرو نوبت آنلاین
            </button>
            <button className="btn-ghost" onClick={onAbout}>
              درباره درمانگاه
            </button>
          </div>
        </div>
      </section>
    </div>
  )
}
