using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Clinic.Api.Data;

/// <summary>
/// Creates the schema on first run, installs PostgreSQL-specific pieces
/// (btree_gist + the no-double-booking exclusion constraint) and seeds the
/// first admin user from configuration.
/// </summary>
public static class DbBootstrapper
{
    // Iranian weekday indices: Saturday=0, Sunday=1, Monday=2, Tuesday=3, Wednesday=4, Thursday=5, Friday=6
    private static readonly (string Name, string Specialty, string Location, int VisitMin, (int Weekday, string From, string To)[] Shifts)[] DoctorSeed =
    [
        // بخش قلب
        ("دکتر بیابانی",     "قلب",                   "", 20, [(1,"04:45","05:45")]),
        ("دکتر صالحی",       "قلب",                   "", 20, [(2,"08:30","10:45"), (4,"08:30","10:45")]),
        ("دکتر قضاوی",       "قلب",                   "", 20, [(3,"13:30","14:45")]),
        // بخش داخلی
        ("دکتر محمد حسینی",  "داخلی",                 "", 20, [(0,"15:30","17:45"), (3,"15:30","17:45")]),
        ("دکتر فرزانگان",    "داخلی",                 "", 20, [(5,"08:45","11:45")]),
        // بخش داخلی عفونی
        ("دکتر بیدی",        "داخلی عفونی",           "", 20, [(1,"11:30","12:30"), (2,"15:30","17:30"), (4,"11:30","12:30")]),
        // بخش گوش و حلق و بینی
        ("دکتر کریمی",       "گوش و حلق و بینی",      "", 20, [(0,"13:15","14:00"), (4,"13:15","14:00")]),
        ("دکتر کلانی",       "گوش و حلق و بینی",      "", 20, [(2,"15:00","16:00")]),
        // بخش مغز و اعصاب
        ("دکتر صالحی",       "مغز و اعصاب",           "", 20, [(0,"10:30","12:00")]),
        ("دکتر طهماسبیان",   "مغز و اعصاب",           "", 20, [(1,"07:50","11:00"), (3,"07:50","11:00")]),
        ("دکتر خوشرو",       "مغز و اعصاب",           "", 20, [(4,"09:30","10:30")]),
        // بخش ارتوپدی
        ("دکتر جلیلی فر",    "ارتوپدی",               "", 20, [(0,"11:30","13:00")]),
        ("دکتر علیزاد",      "ارتوپدی",               "", 20, [(1,"14:45","16:30")]),
        ("دکتر علی مددی",    "ارتوپدی",               "", 20, [(2,"15:00","18:00"), (4,"15:00","18:00")]),
        ("دکتر مهدوی",       "ارتوپدی",               "", 20, [(3,"17:30","19:00")]),
        // بخش زنان
        ("دکتر سلطانی",      "زنان",                  "", 20, [(1,"14:00","16:00")]),
        ("دکتر یاوری",       "زنان",                  "", 20, [(2,"07:30","09:30")]),
        ("دکتر اسلامی",      "زنان",                  "", 20, [(3,"09:00","10:30")]),
        ("دکتر حسنی",        "زنان",                  "", 20, [(4,"08:50","11:00")]),
        // بخش چشم
        ("دکتر شیوخی",       "چشم‌پزشکی",             "", 20, [(1,"08:45","11:00"), (4,"10:30","12:00")]),
        // بخش بینایی سنجی
        ("آقای بهترک",       "بینایی‌سنجی",            "", 20, [(0,"17:00","19:00"), (2,"17:00","19:00"), (4,"17:00","19:00")]),
        // بخش روانپزشکی
        ("دکتر رزاقی",       "روانپزشکی",             "", 30, [(0,"16:30","18:00")]),
        // بخش پوست
        ("دکتر غفاری",       "پوست",                  "", 20, [(3,"16:30","17:45")]),
        // بخش کلیه و مجاری ادراری
        ("دکتر وزیرنیا",     "کلیه و مجاری ادراری",   "", 20, [(2,"07:30","08:00"), (4,"07:30","08:00")]),
        // بخش تغذیه
        ("دکتر هاشمی",       "تغذیه",                 "", 20, [(2,"16:30","18:00")]),
        // بخش دیابت
        ("دکتر مهدیخواه",    "دیابت",                 "", 20, [(2,"09:00","11:30")]),
    ];

    private static async Task SeedDoctorsAsync(AppDbContext db)
    {
        if (await db.Doctors.AnyAsync()) return;

        foreach (var (name, specialty, location, visitMin, shifts) in DoctorSeed)
        {
            var doctor = new Domain.Doctor
            {
                FullName = name,
                Specialty = specialty,
                Location = location,
                DefaultVisitMinutes = visitMin,
                IsActive = true,
                Schedules = shifts.Select(s => new Domain.DoctorSchedule
                {
                    Weekday = s.Weekday,
                    StartTime = TimeOnly.Parse(s.From),
                    EndTime = TimeOnly.Parse(s.To),
                }).ToList(),
            };
            db.Doctors.Add(doctor);
        }
        await db.SaveChangesAsync();
    }

    private static readonly HashSet<string> KnownWeakPasswords =
    [
        "admin123", "admin", "password", "password123", "123456789",
        "1234567890", "changeme", "clinic", "clinic123", "qwerty12345",
    ];

    /// <summary>SQLite/local demo path — no extensions, no exclusion constraint.</summary>
    public static async Task InitializeSqliteAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureColumnsExistSqliteAsync(db);
        logger.LogInformation("Database schema ready (SQLite).");
        await SeedDoctorsAsync(db);
        await SeedAdminAsync(db, config, logger);
    }

    // EnsureCreatedAsync only runs once (no migrations). If columns were added to
    // the domain after the DB was first created they will be absent and writes will
    // throw a 500. Add them idempotently here so the schema stays in sync.
    private static async Task EnsureColumnsExistSqliteAsync(AppDbContext db)
    {
        // SQLite supports "ADD COLUMN IF NOT EXISTS" since 3.37.0.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE doctors ADD COLUMN IF NOT EXISTS default_visit_minutes INTEGER NOT NULL DEFAULT 30;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE doctors ADD COLUMN IF NOT EXISTS location TEXT NOT NULL DEFAULT '';");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE doctor_schedules ADD COLUMN IF NOT EXISTS is_active INTEGER NOT NULL DEFAULT 1;");
    }

    private static async Task SeedAdminAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync()) return;
        var username = config["AdminSeed:Username"] ?? "admin";
        var password = config["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException(
                "AdminSeed__Password is required to seed the first admin (minimum 12 characters). " +
                "Set it via the environment and restart.");
        if (KnownWeakPasswords.Contains(password))
            throw new InvalidOperationException(
                $"Refusing to seed admin '{username}' with a well-known default password. Choose a strong one.");
        db.AdminUsers.Add(new Domain.AdminUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded initial admin '{Username}'.", username);
    }

    public static async Task InitializeAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");

        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'appointments_no_overlap') THEN
                    ALTER TABLE appointments ADD CONSTRAINT appointments_no_overlap
                        EXCLUDE USING gist (
                            doctor_id WITH =,
                            tstzrange(starts_at, ends_at) WITH &&
                        )
                        WHERE (status = 0);
                END IF;
            END $$;
            """);

        // EnsureCreatedAsync only runs once (no migrations). Columns added to the
        // domain after the DB was first created must be backfilled idempotently.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE doctors
                ADD COLUMN IF NOT EXISTS default_visit_minutes INTEGER NOT NULL DEFAULT 30,
                ADD COLUMN IF NOT EXISTS location VARCHAR(100) NOT NULL DEFAULT '';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE doctor_schedules
                ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;
            """);

        logger.LogInformation("Database schema ready (exclusion constraint ensured).");

        await SeedDoctorsAsync(db);
        await SeedAdminAsync(db, config, logger);
    }
}
