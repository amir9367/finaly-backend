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
        ("دکتر مهابخواه",    "دیابت و غدد",          "طبقه اول",  20, [(0,"08:00","13:00"), (2,"08:00","13:00")]),
        ("دکتر جلیلی‌فر",    "ارتوپدی",              "طبقه دوم",  20, [(0,"08:00","13:00")]),
        ("دکتر صالحی",       "مغز و اعصاب",           "زیرزمین",   20, [(0,"08:00","13:00")]),
        ("دکتر کریمی",       "گوش، حلق و بینی",       "طبقه اول",  20, [(0,"14:00","18:00"), (4,"14:00","18:00")]),
        ("دکتر محمد حسینی",  "داخلی",                 "طبقه دوم",  20, [(0,"14:00","18:00"), (3,"14:00","18:00")]),
        ("دکتر رزاقی",       "روانپزشکی",             "زیرزمین",   30, [(0,"14:00","18:00")]),
        ("آقای بهترک",       "بینایی‌سنجی",           "زیرزمین",   20, [(0,"14:00","18:00"), (2,"14:00","18:00"), (4,"14:00","18:00")]),
        ("دکتر بیدی",        "داخلی",                 "طبقه اول",  20, [(1,"08:00","13:00"), (4,"08:00","13:00"), (2,"14:00","18:00")]),
        ("دکتر طهماسبیان",   "مغز و اعصاب",           "طبقه دوم",  20, [(1,"08:00","13:00"), (3,"08:00","13:00")]),
        ("دکتر شبوخی",       "چشم‌پزشکی",             "زیرزمین",   20, [(1,"08:00","13:00"), (4,"08:00","13:00")]),
        ("دکتر سلطانی",      "زنان و زایمان",          "زیرزمین",   20, [(1,"08:00","13:00")]),
        ("دکتر بیاناتی",     "قلب و عروق",            "زیرزمین",   20, [(1,"08:00","13:00")]),
        ("دکتر باوری",       "زنان و زایمان",          "طبقه دوم",  20, [(2,"08:00","13:00")]),
        ("دکتر وزیرنیا",     "اورولوژی",              "زیرزمین",   20, [(2,"08:00","13:00"), (4,"08:00","13:00")]),
        ("دکتر صالحی",       "قلب",                   "زیرزمین",   20, [(2,"08:00","13:00"), (4,"08:00","13:00")]),
        ("دکتر علی مددی",    "ارتوپدی",              "طبقه اول",  20, [(2,"14:00","18:00"), (4,"14:00","18:00")]),
        ("دکتر کلانی",       "گوش، حلق و بینی",       "طبقه دوم",  20, [(2,"14:00","18:00")]),
        ("دکتر هاشمی",       "تغذیه و رژیم‌درمانی",   "زیرزمین",   20, [(2,"14:00","18:00")]),
        ("دکتر اسلامی",      "زنان و زایمان",          "طبقه اول",  20, [(3,"08:00","13:00")]),
        ("دکتر مهدوی",       "ارتوپدی",              "طبقه اول",  20, [(3,"14:00","18:00")]),
        ("دکتر غفاری",       "پوست و مو",             "طبقه دوم",  20, [(3,"14:00","18:00")]),
        ("دکتر قضاوی",       "قلب و عروق",            "زیرزمین",   20, [(3,"14:00","18:00")]),
        ("دکتر حسنی",        "زنان و زایمان",          "طبقه دوم",  20, [(4,"08:00","13:00")]),
        ("دکتر خوشرو",       "مغز و اعصاب",           "زیرزمین",   20, [(4,"08:00","13:00")]),
        ("دکتر فرزاندگان",   "داخلی",                 "طبقه دوم",  20, [(5,"08:00","13:00")]),
        ("خانم علیمردانی",   "شنوایی‌سنجی",           "زیرزمین",   20, [(5,"08:00","13:00")]),
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
        logger.LogInformation("Database schema ready (SQLite).");
        await SeedDoctorsAsync(db);
        await SeedAdminAsync(db, config, logger);
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
        logger.LogInformation("Database schema ready (exclusion constraint ensured).");

        await SeedDoctorsAsync(db);
        await SeedAdminAsync(db, config, logger);
    }
}
