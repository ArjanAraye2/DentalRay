using System.Text.RegularExpressions;
using DentalRay.Api.Models;
using Microsoft.Extensions.Options;

namespace DentalRay.Api.Services
{
    // ============================================================
    // RadiologyStorageService
    // ============================================================
    //
    // مسئول ذخیره و مدیریت فیزیکی تصاویر رادیولوژی روی Disk است.
    //
    // مسیر Root از Configuration خوانده می‌شود و Hard-code نیست.
    //
    // ساختار فعلی:
    //
    // D:\RadiologyData\
    //      1860271855_4_001.jpg
    //      1860271855_4_002.jpg
    //      1860271855_4_003.png
    //
    // ============================================================

    public class RadiologyStorageService
    {
        // --------------------------------------------------------
        // مسیر نهایی و کامل Root
        // --------------------------------------------------------

        private readonly string _rootPath;


        // ========================================================
        // Constructor
        // ========================================================

        public RadiologyStorageService(
            IOptions<RadiologyStorageOptions> storageOptions)
        {
            if (storageOptions == null ||
                storageOptions.Value == null)
            {
                throw new InvalidOperationException(
                    "Radiology storage configuration is missing.");
            }


            string configuredRootPath =
                storageOptions.Value.RootPath;


            if (string.IsNullOrWhiteSpace(
                configuredRootPath))
            {
                throw new InvalidOperationException(
                    "Radiology storage RootPath is not configured.");
            }


            // ----------------------------------------------------
            // تبدیل به مسیر کامل
            // ----------------------------------------------------

            _rootPath =
                Path.GetFullPath(
                    configuredRootPath);


            // ----------------------------------------------------
            // ایجاد پوشه در صورت عدم وجود
            // ----------------------------------------------------
            //
            // اگر مسیر نامعتبر باشد یا دسترسی نوشتن وجود نداشته
            // باشد، خطا در همین ابتدای کار مشخص می‌شود.
            //
            Directory.CreateDirectory(
                _rootPath);
        }


        // ========================================================
        // GetRootPath
        // ========================================================

        public string GetRootPath()
        {
            return _rootPath;
        }


        // ========================================================
        // SaveImageAsync
        // ========================================================

        public async Task<string> SaveImageAsync(
            Stream imageStream,
            string originalFileName,
            string nationalCode,
            int studyID,
            int serial)
        {
            // ----------------------------------------------------
            // Validation
            // ----------------------------------------------------

            if (imageStream == null)
            {
                throw new ArgumentNullException(
                    nameof(imageStream));
            }


            if (string.IsNullOrWhiteSpace(
                originalFileName))
            {
                throw new ArgumentException(
                    "Image file name is required.",
                    nameof(originalFileName));
            }


            if (string.IsNullOrWhiteSpace(
                nationalCode))
            {
                throw new ArgumentException(
                    "NationalCode is required.",
                    nameof(nationalCode));
            }


            if (studyID <= 0)
            {
                throw new ArgumentException(
                    "StudyID must be greater than zero.",
                    nameof(studyID));
            }


            if (serial <= 0 ||
                serial > 999)
            {
                throw new ArgumentException(
                    "Serial must be between 1 and 999.",
                    nameof(serial));
            }


            // ----------------------------------------------------
            // NationalCode فقط عدد
            // ----------------------------------------------------

            if (!Regex.IsMatch(
                nationalCode,
                @"^\d+$"))
            {
                throw new ArgumentException(
                    "NationalCode must contain only digits.",
                    nameof(nationalCode));
            }


            // ====================================================
            // Extension
            // ====================================================

            string extension =
                Path.GetExtension(
                    originalFileName)
                .ToLowerInvariant();


            if (extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png")
            {
                throw new InvalidOperationException(
                    "Only JPG, JPEG and PNG image files are allowed.");
            }


            // ====================================================
            // FileName
            // ====================================================

            string fileName =
                $"{nationalCode}_{studyID}_{serial:000}{extension}";


            // ====================================================
            // Physical Path
            // ====================================================

            string physicalFilePath =
                GetPhysicalPath(
                    fileName);


            // ====================================================
            // جلوگیری از Overwrite
            // ====================================================

            if (File.Exists(
                physicalFilePath))
            {
                throw new IOException(
                    $"The image file already exists: {fileName}");
            }


            // ====================================================
            // ذخیره فایل
            // ====================================================

            await using FileStream fileStream =
                new FileStream(
                    physicalFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);


            await imageStream.CopyToAsync(
                fileStream);


            // چون فایل مستقیماً زیر RootPath است،
            // RelativePath همان FileName است.
            return fileName;
        }


        // ========================================================
        // RenameImage
        // ========================================================
        //
        // برای Merge و تغییر NationalCode استفاده می‌شود.
        //
        // ========================================================

        public void RenameImage(
            string oldRelativePath,
            string newFileName)
        {
            if (string.IsNullOrWhiteSpace(
                oldRelativePath))
            {
                throw new ArgumentException(
                    "Old RelativePath is required.",
                    nameof(oldRelativePath));
            }


            if (string.IsNullOrWhiteSpace(
                newFileName))
            {
                throw new ArgumentException(
                    "New file name is required.",
                    nameof(newFileName));
            }


            // ----------------------------------------------------
            // مسیر قدیمی باید فقط نام فایل باشد
            // ----------------------------------------------------

            string oldFileName =
                Path.GetFileName(
                    oldRelativePath);


            if (!string.Equals(
                oldFileName,
                oldRelativePath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Old RelativePath must refer to a file directly under the storage root.");
            }


            // ----------------------------------------------------
            // نام فایل جدید باید مطابق Convention باشد
            // ----------------------------------------------------

            if (!Regex.IsMatch(
                newFileName,
                @"^\d+_\d+_\d{3}\.(jpg|jpeg|png)$",
                RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException(
                    "Invalid new image file name.");
            }


            // ----------------------------------------------------
            // مسیرهای امن
            // ----------------------------------------------------

            string oldPhysicalPath =
                GetPhysicalPath(
                    oldFileName);


            string newPhysicalPath =
                GetPhysicalPath(
                    newFileName);


            // ----------------------------------------------------
            // بررسی فایل مبدأ
            // ----------------------------------------------------

            if (!File.Exists(
                oldPhysicalPath))
            {
                throw new FileNotFoundException(
                    "The source image file was not found.",
                    oldPhysicalPath);
            }


            // ----------------------------------------------------
            // جلوگیری از Overwrite
            // ----------------------------------------------------

            if (File.Exists(
                newPhysicalPath))
            {
                throw new IOException(
                    $"The destination image file already exists: {newFileName}");
            }


            // ----------------------------------------------------
            // Rename
            // ----------------------------------------------------

            File.Move(
                oldPhysicalPath,
                newPhysicalPath);
        }


        // ========================================================
        // GetPhysicalPath
        // ========================================================
        //
        // فقط یک FileName مستقیم زیر RootPath قبول می‌شود.
        //
        // این کنترل از Path Traversal جلوگیری می‌کند.
        //
        // ========================================================

        public string GetPhysicalPath(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(
                fileName))
            {
                throw new ArgumentException(
                    "FileName is required.",
                    nameof(fileName));
            }


            // ----------------------------------------------------
            // فقط نام فایل مجاز است
            // ----------------------------------------------------

            string safeFileName =
                Path.GetFileName(
                    fileName);


            if (!string.Equals(
                safeFileName,
                fileName,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "FileName must refer to a file directly under the storage root.");
            }


            // ----------------------------------------------------
            // ساخت مسیر کامل
            // ----------------------------------------------------

            string fullPath =
                Path.GetFullPath(
                    Path.Combine(
                        _rootPath,
                        safeFileName));


            // ----------------------------------------------------
            // Safety Check
            // ----------------------------------------------------
            //
            // مسیر نهایی باید داخل RootPath باقی بماند.
            //
            string rootWithSeparator =
                _rootPath
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                +
                Path.DirectorySeparatorChar;


            if (!fullPath.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Resolved path is outside the storage root.");
            }


            return fullPath;
        }
    }
}