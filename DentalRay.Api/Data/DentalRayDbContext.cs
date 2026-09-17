using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Data
{
    // ============================================================
    // DentalRayDbContext
    // ============================================================
    // درگاه EF Core برای دیتابیس DentalRay.
    // ============================================================
    public class DentalRayDbContext : DbContext
    {
        public DentalRayDbContext(DbContextOptions<DentalRayDbContext> options)
            : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<RadiologyStudy> RadiologyStudies { get; set; }
        public DbSet<RadiologyImage> RadiologyImages { get; set; }

        // جدول واسط Many-to-Many بین Study و Image.
        public DbSet<RadiologyStudyImage> RadiologyStudyImages { get; set; }

        // دندان‌های انتخاب‌شده برای هر Study بر اساس شماره‌گذاری FDI.
        public DbSet<RadiologyStudyTooth> RadiologyStudyTeeth { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Patient 1 -> N Studies
            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(study => study.PatientID)
                .OnDelete(DeleteBehavior.NoAction);

            // Patient 1 -> N Images
            // Image دیگر مستقیماً متعلق به Study نیست.
            modelBuilder.Entity<RadiologyImage>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(image => image.PatientID)
                .OnDelete(DeleteBehavior.NoAction);

            // هر Patient یک فضای Serial مستقل دارد.
            modelBuilder.Entity<RadiologyImage>()
                .HasIndex(image => new { image.PatientID, image.SerialNumber })
                .IsUnique();

            // هر Pair فقط یک بار می‌تواند وجود داشته باشد؛ بنابراین
            // Attach تکراری همان Image به همان Study غیرممکن می‌شود.
            modelBuilder.Entity<RadiologyStudyImage>()
                .HasKey(link => new { link.StudyID, link.ImageID });

            modelBuilder.Entity<RadiologyStudyImage>()
                .HasOne<RadiologyStudy>()
                .WithMany()
                .HasForeignKey(link => link.StudyID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudyImage>()
                .HasOne<RadiologyImage>()
                .WithMany()
                .HasForeignKey(link => link.ImageID)
                .OnDelete(DeleteBehavior.NoAction);

            // هر دندان فقط یک بار در هر Study ثبت می‌شود.
            // کلید مرکب دقیقاً با Primary Key جدول SQL هماهنگ است.
            modelBuilder.Entity<RadiologyStudyTooth>()
                .HasKey(x => new { x.StudyID, x.ToothNumber });

            modelBuilder.Entity<RadiologyStudyTooth>()
                .HasOne<RadiologyStudy>()
                .WithMany()
                .HasForeignKey(x => x.StudyID)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
