using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Data
{
    // ============================================================
    // DentalRayDbContext
    // ============================================================
    // EF Core gateway for the DentalRay database.
    // ============================================================
    public class DentalRayDbContext : DbContext
    {
        public DentalRayDbContext(DbContextOptions<DentalRayDbContext> options) : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<RadiologyStudy> RadiologyStudies { get; set; }
        public DbSet<RadiologyImage> RadiologyImages { get; set; }
        public DbSet<RadiologyStudyImage> RadiologyStudyImages { get; set; }
        public DbSet<RadiologyStudyTooth> RadiologyStudyTeeth { get; set; }

        // Lookup values used by the Study form.
        public DbSet<StudyType> StudyTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<Patient>().WithMany()
                .HasForeignKey(study => study.PatientID)
                .OnDelete(DeleteBehavior.NoAction);

            // Every StudyTypeID must point to a valid row in tblStudyTypes.
            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<StudyType>().WithMany()
                .HasForeignKey(study => study.StudyTypeID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyImage>()
                .HasOne<Patient>().WithMany()
                .HasForeignKey(image => image.PatientID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyImage>()
                .HasIndex(image => new { image.PatientID, image.SerialNumber })
                .IsUnique();

            modelBuilder.Entity<RadiologyStudyImage>()
                .HasKey(link => new { link.StudyID, link.ImageID });

            modelBuilder.Entity<RadiologyStudyImage>()
                .HasOne<RadiologyStudy>().WithMany()
                .HasForeignKey(link => link.StudyID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudyImage>()
                .HasOne<RadiologyImage>().WithMany()
                .HasForeignKey(link => link.ImageID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudyTooth>()
                .HasKey(x => new { x.StudyID, x.ToothNumber });

            modelBuilder.Entity<RadiologyStudyTooth>()
                .HasOne<RadiologyStudy>().WithMany()
                .HasForeignKey(x => x.StudyID)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
