using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Data
{
    public class DentalRayDbContext : DbContext
    {
        public DentalRayDbContext(
            DbContextOptions<DentalRayDbContext> options)
            : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<RadiologyStudy> RadiologyStudies { get; set; }
        public DbSet<RadiologyImage> RadiologyImages { get; set; }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Person> Persons { get; set; }
        public DbSet<OrganizationMember> OrganizationMembers { get; set; }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(study => study.PatientID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(study => study.OrganizationID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<Person>()
                .WithMany()
                .HasForeignKey(study => study.DentistPersonID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RadiologyImage>()
                .HasOne<RadiologyStudy>()
                .WithMany()
                .HasForeignKey(image => image.StudyID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AppUser>()
                .HasIndex(user => user.UserName)
                .IsUnique();

            modelBuilder.Entity<AuditLog>()
                .HasIndex(log => log.CreatedDate);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(log => log.UserID);

            modelBuilder.Entity<Organization>()
                .HasIndex(x => x.OrganizationGuid)
                .IsUnique();

            modelBuilder.Entity<Person>()
                .HasIndex(x => x.PersonGuid)
                .IsUnique();

            modelBuilder.Entity<OrganizationMember>()
                .HasIndex(x => new { x.OrganizationID, x.PersonID })
                .IsUnique();

            modelBuilder.Entity<OrganizationMember>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<OrganizationMember>()
                .HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.PersonID)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
