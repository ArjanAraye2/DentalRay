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
        public DbSet<Clinic> Clinics { get; set; }
        public DbSet<ClinicStaff> ClinicStaff { get; set; }
        public DbSet<DentalSpecialty> DentalSpecialties { get; set; }
        public DbSet<ImageType> ImageTypes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserDentist> UserDentists { get; set; }
        public DbSet<StudyAction> StudyActions { get; set; }
        public DbSet<StudyPayment> StudyPayments { get; set; }
        public DbSet<PosSetting> PosSettings { get; set; }
        public DbSet<PatientMessage> PatientMessages { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<WaitStage> WaitStages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RadiologyStudy>().HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyStudy>().HasOne<StudyType>().WithMany().HasForeignKey(x => x.StudyTypeID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyImage>().HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyImage>().HasOne<ImageType>().WithMany().HasForeignKey(x => x.ImageTypeID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyImage>().HasIndex(x => new { x.PatientID, x.SerialNumber }).IsUnique();

            modelBuilder.Entity<RadiologyStudyImage>().HasKey(x => new { x.StudyID, x.ImageID });
            modelBuilder.Entity<RadiologyStudyImage>().HasOne<RadiologyStudy>().WithMany().HasForeignKey(x => x.StudyID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RadiologyStudyImage>().HasOne<RadiologyImage>().WithMany().HasForeignKey(x => x.ImageID).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudyTooth>().HasKey(x => new { x.StudyID, x.ToothNumber });
            modelBuilder.Entity<RadiologyStudyTooth>().HasOne<RadiologyStudy>().WithMany().HasForeignKey(x => x.StudyID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StudyAction>().HasOne<RadiologyStudy>().WithMany().HasForeignKey(x => x.StudyID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StudyPayment>().HasOne<RadiologyStudy>().WithMany().HasForeignKey(x => x.StudyID).OnDelete(DeleteBehavior.NoAction);

            // Clinic membership is a many-to-many relationship represented by tblClinicStaff.
            modelBuilder.Entity<ClinicStaff>().HasKey(x => new { x.ClinicID, x.StaffID });
            modelBuilder.Entity<ClinicStaff>().HasOne<Clinic>().WithMany().HasForeignKey(x => x.ClinicID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ClinicStaff>().HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffID).OnDelete(DeleteBehavior.NoAction);

            // Specialty is optional for employees and is used for dentists.
            modelBuilder.Entity<Staff>().HasOne<DentalSpecialty>().WithMany().HasForeignKey(x => x.SpecialtyID).OnDelete(DeleteBehavior.NoAction);

            // Every DentalRay User belongs to exactly one Staff record.
            modelBuilder.Entity<User>().HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffID).OnDelete(DeleteBehavior.NoAction);

            // Employee access to dentists is clinic-specific. Keeping the same
            // composite key as SQL prevents duplicate assignments.
            modelBuilder.Entity<UserDentist>().HasKey(x => new { x.UserID, x.ClinicID, x.DentistStaffID });
            modelBuilder.Entity<UserDentist>().HasOne<User>().WithMany().HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserDentist>().HasOne<Staff>().WithMany().HasForeignKey(x => x.DentistStaffID).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
