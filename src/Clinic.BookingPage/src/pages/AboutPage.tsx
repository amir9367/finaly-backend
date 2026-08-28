const DEPARTMENTS = [
  { name: 'آزمایشگاه', desc: 'انجام انواع آزمایش‌های خون، ادرار و بیوشیمی — همه روزه حتی تعطیلات', tel: '۰۲۱۳۳۱۰۱۴۷۸' },
  { name: 'دندانپزشکی', desc: 'خدمات عمومی و تخصصی، ترمیم، جرم‌گیری و ایمپلنت', tel: '۰۲۱۳۳۱۰۱۴۶۰' },
  { name: 'فیزیوتراپی', desc: 'توانبخشی و درمان دردهای اسکلتی-عضلانی با روش‌های نوین' },
  { name: 'داخلی · گوارش · دیابت', desc: 'تشخیص و درمان بیماری‌های داخلی، گوارشی و کنترل دیابت' },
  { name: 'عفونی · ریه · اسپیرومتری', desc: 'بیماری‌های عفونی، تنفسی و تست عملکرد ریه' },
  { name: 'قلب و عروق', desc: 'ویزیت تخصصی قلب و درمان بیماری‌های قلبی-عروقی' },
  { name: 'هولتر قلب · هولتر فشار', desc: 'پایش ۲۴ ساعته ضربان قلب و فشار خون' },
  { name: 'اکوکاردیوگرافی · نوار قلب', desc: 'سونوگرافی قلب (اکو) و ثبت نوار قلب (ECG)' },
  { name: 'ارتوپدی', desc: 'بیماری‌های استخوان، مفاصل، ستون فقرات و شکستگی' },
  { name: 'روانپزشکی', desc: 'درمان اختلالات روانی، اضطراب و افسردگی' },
  { name: 'روانشناسی · مشاوره', desc: 'مشاوره فردی، خانوادگی و زوج‌درمانی' },
  { name: 'مغز و اعصاب · نوار عصب و عضله', desc: 'بیماری‌های مغز و اعصاب، انجام EMG/NCV' },
  { name: 'گوش · حلق · بینی · شنوایی‌سنجی', desc: 'ویزیت تخصصی ENT و ارزیابی کامل شنوایی' },
  { name: 'چشم‌پزشکی · بینایی‌سنجی', desc: 'معاینه چشم، تجویز عینک و درمان بیماری‌های چشمی' },
  { name: 'تغذیه · رژیم درمانی', desc: 'مشاوره تغذیه و تنظیم رژیم برای کاهش وزن' },
  { name: 'کلیه · مجاری ادراری', desc: 'تشخیص و درمان بیماری‌های کلیه و مجاری ادراری' },
  { name: 'زنان و زایمان', desc: 'مراقبت بارداری، زایمان و بهداشت بانوان' },
  { name: 'رادیولوژی · سونوگرافی · OPG', desc: 'تصویربرداری، سونوگرافی و سنجش تراکم استخوان', tel: '۰۲۱۳۳۰۵۹۰۳۴' },
]

const INSURANCES = [
  'تأمین اجتماعی', 'سلامت', 'نیروهای مسلح', 'دی', 'بنیاد شهید و ایثارگران',
  'دانا', 'ایران', 'آسیا', 'سینا', 'آتیه‌سازان', 'رازی', 'پارسیان',
]

export default function AboutPage() {
  return (
    <>
      <header className="hero hero-sm">
        <h1>درباره درمانگاه طب الرضا(ع)</h1>
        <p>درمانگاه شبانه‌روزی با کادر پزشکی مجرب در خدمت شما</p>
      </header>

      <main className="page-main">
        <div className="content-wrap">

          <div className="about-cards">
            <div className="info-card">
              <h3 className="info-card-title">درباره ما</h3>
              <p>
                درمانگاه شبانه‌روزی طب الرضا(ع) با بیش از یک دهه سابقه، با برخورداری از پزشکان متخصص و تجهیزات پیشرفته پزشکی، در تمام ساعات شبانه‌روز آماده ارائه خدمات درمانی به بیماران است.
              </p>
            </div>
            <div className="info-card">
              <h3 className="info-card-title">آدرس</h3>
              <p>
                تهران، پیروزی — بلوار ابوذر، پل دوم<br />
                خیابان ائمه اطهار، نبش برادران باقری، پلاک ۲۳
              </p>
            </div>
            <div className="info-card">
              <h3 className="info-card-title">ساعات کاری</h3>
              <div className="hours-row">صبح‌ها (همه روزه بجز تعطیلات): ۸:۰۰ تا ۱۳:۰۰</div>
              <div className="hours-row">عصرها (بجز پنجشنبه و تعطیلات): ۱۴:۰۰ تا ۱۸:۰۰</div>
              <div className="hours-row">اورژانس: ۲۴ ساعته</div>
            </div>
            <div className="info-card">
              <h3 className="info-card-title">تماس با ما</h3>
              <p>
                وقت‌دهی عمومی: <a href="tel:34246" style={{ color: 'var(--gm)', fontWeight: 700 }}>📞 ۳۴۲۴۶</a><br />
                آزمایشگاه: <a href="tel:03133101478" style={{ color: 'var(--gm)', fontWeight: 700 }}>📞 ۰۳۱۳۳۱۰۱۴۷۸</a><br />
                دندانپزشکی: <a href="tel:03133101460" style={{ color: 'var(--gm)', fontWeight: 700 }}>📞 ۰۳۱۳۳۱۰۱۴۶۰</a><br />
                رادیولوژی: <a href="tel:03133059034" style={{ color: 'var(--gm)', fontWeight: 700 }}>📞 ۰۳۱۳۳۰۵۹۰۳۴</a>
              </p>
            </div>
          </div>

          <section className="departments">
            <div className="section-title">
              <h2>بخش‌های پزشکی</h2>
              <p>خدمات تخصصی با کادر پزشکی مجرب</p>
            </div>
            <div className="dept-grid">
              {DEPARTMENTS.map((d) => (
                <div className="dept-card" key={d.name}>
                  <h4>{d.name}</h4>
                  <p>{d.desc}</p>
                  {d.tel && (
                    <a className="dept-tel"
                      href={`tel:${d.tel.replace(/[۰-۹]/g, (ch) => '۰۱۲۳۴۵۶۷۸۹'.indexOf(ch).toString())}`}>
                      📞 {d.tel}
                    </a>
                  )}
                </div>
              ))}
            </div>
          </section>

          <section className="insurance-box">
            <div className="insurance-title">طرف قرارداد با بیمه‌های معتبر</div>
            <div className="insurance-badges">
              {INSURANCES.map((i) => (
                <span className="insurance-badge" key={i}>{i}</span>
              ))}
            </div>
          </section>

        </div>
      </main>
    </>
  )
}
