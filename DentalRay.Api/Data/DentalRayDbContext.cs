using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Data
{
    // EF Core gateway for the DentalRay database.
    public class DentalRayDbContext : DbContext
    {
        public DentalRayDbContext(DbContextOptions<DentalRayDbContext> options) : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<RadiologyStudy> RadiologyStudies { get; set; }
        public DbSet<RadiologyImage> RadiologyImages { get; set; }
        public DbSet<RadiologyStudyImage> RadiologyStudyImages { get; set; }
        public DbSet<RadiologyStudyTooth> RadiologyStudyTeeth { get; set; }
        public DbSet<StudyType> StudyTypes { get; set; }
        public DbSet<Staff> Staff { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RadiologyStudy>().HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyStudy>().HasOne<StudyType>().WithMany().HasForeignKey(x => x.StudyTypeID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyImage>().HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyImage>().HasIndex(x => new { x.PatientID, x.SerialNumber }).IsUnique();

            modelBuilder.Entity<RadiologyStudyImage>().HasKey(x => new { x.StudyID, x.ImageID });
            modelBuilder.Entity<RadiologyStudyImage>().HasOne<RadiologyStudy>().WithMany().HasForeignKey(x => x.StudyID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyStudyImage>().HasOne<RadiologyImage>().WithMany().HasForeignKey(x => x.ImageID).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudyTooth>().HasKey(x => new { x.StudyID, x.ToothNumber });
            modelBuilder.Entity<RadiologyStudyTooth>().HasOne<RadiologyStudy>().WithMany().HasForeignKey(x => x.StudyID).OnDelete(DeleteBehavior.NoAction);

            // Every DentalRay User belongs to exactly one Staff record.
            // StaffID and UserName are unique in the SQL schema as well.
            modelBuilder.Entity<User>().HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffID).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
