using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Data
{
    // ============================================================
    // DentalRayDbContext
    // ============================================================
    //
    // این کلاس درگاه ارتباط برنامه DentalRay
    // با دیتابیس SQL Server است.
    //
    // EF Core از این کلاس برای:
    //
    // - خواندن اطلاعات
    // - اضافه کردن اطلاعات
    // - ویرایش اطلاعات
    // - حذف اطلاعات
    // - مدیریت ارتباط بین جدول‌ها
    //
    // استفاده می‌کند.
    public class DentalRayDbContext : DbContext
    {
        // --------------------------------------------------------
        // Constructor
        // --------------------------------------------------------
        //
        // تنظیمات مربوط به DbContext از طریق
        // DbContextOptions به این کلاس ارسال می‌شود.
        //
        // این تنظیمات در Program.cs ایجاد شده‌اند.
        public DentalRayDbContext(
            DbContextOptions<DentalRayDbContext> options)
            : base(options)
        {
        }


        // ========================================================
        // tblPatients
        // ========================================================
        //
        // این DbSet نماینده جدول:
        //
        // tblPatients
        //
        // در SQL Server است.
        //
        // کلاس Patient نماینده هر رکورد این جدول است.
        public DbSet<Patient> Patients { get; set; }


        // ========================================================
        // tblRadiologyStudies
        // ========================================================
        //
        // این DbSet نماینده جدول:
        //
        // tblRadiologyStudies
        //
        // است.
        //
        // هر RadiologyStudy متعلق به یک Patient است.
        public DbSet<RadiologyStudy> RadiologyStudies { get; set; }


        // ========================================================
        // tblRadiologyImages
        // ========================================================
        //
        // این DbSet نماینده جدول:
        //
        // tblRadiologyImages
        //
        // است.
        //
        // هر تصویر متعلق به یک RadiologyStudy است.
        public DbSet<RadiologyImage> RadiologyImages { get; set; }


        // ========================================================
        // OnModelCreating
        // ========================================================
        //
        // در این متد روابط بین Entityها را به‌صورت صریح
        // برای EF Core تعریف می‌کنیم.
        //
        // این کار باعث می‌شود ساختار رابطه‌ها برای EF Core
        // کاملاً مشخص باشد.
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            // ====================================================
            // رابطه Patient → RadiologyStudy
            // ====================================================
            //
            // یک بیمار می‌تواند چندین مطالعه رادیولوژی داشته باشد.
            //
            // Patient       1
            //    |
            //    | 
            //    N
            // RadiologyStudy
            //
            modelBuilder.Entity<RadiologyStudy>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(study => study.PatientID)
                .OnDelete(DeleteBehavior.NoAction);


            // ====================================================
            // رابطه RadiologyStudy → RadiologyImage
            // ====================================================
            //
            // یک Study می‌تواند چندین تصویر داشته باشد.
            //
            // RadiologyStudy       1
            //       |
            //       |
            //       N
            // RadiologyImage
            //
            modelBuilder.Entity<RadiologyImage>()
                .HasOne<RadiologyStudy>()
                .WithMany()
                .HasForeignKey(image => image.StudyID)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}