using System.Globalization;
using System.Text.RegularExpressions;
using DentalRay.Api.Models;
using Microsoft.Extensions.Options;

namespace DentalRay.Api.Services
{
    public class RadiologyStorageService
    {
        private readonly string _rootPath;

        public RadiologyStorageService(IOptions<RadiologyStorageOptions> options)
        {
            if (options?.Value == null || string.IsNullOrWhiteSpace(options.Value.RootPath))
                throw new InvalidOperationException("Radiology storage RootPath is not configured.");

            _rootPath = Path.GetFullPath(options.Value.RootPath);
            Directory.CreateDirectory(_rootPath);
        }

        public string GetRootPath() => _rootPath;

        // Patient folder is created only when the first file is actually saved.
        public async Task<string> SaveImageAsync(Stream stream, string sourceFileName,
            string nationalCode, DateTime uploadDate, int serial)
        {
            ValidateNationalCode(nationalCode);
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (serial <= 0) throw new ArgumentOutOfRangeException(nameof(serial));

            string extension = Path.GetExtension(sourceFileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".pdf"))
                throw new InvalidOperationException("Only JPG, JPEG, PNG and PDF files are allowed.");

            // Database dates remain DateTime. Jalali is used only in the generated filename.
            var pc = new PersianCalendar();
            string jalaliDate = $"{pc.GetYear(uploadDate):0000}{pc.GetMonth(uploadDate):00}{pc.GetDayOfMonth(uploadDate):00}";
            string fileName = $"{nationalCode}_{jalaliDate}_{serial:000}{extension}";
            string relativePath = Path.Combine(nationalCode, fileName);
            string physicalPath = GetPhysicalPath(relativePath);

            string? folder = Path.GetDirectoryName(physicalPath);
            if (folder == null) throw new InvalidOperationException("Patient folder could not be resolved.");
            Directory.CreateDirectory(folder);

            if (File.Exists(physicalPath)) throw new IOException($"File already exists: {fileName}");
            await using var output = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(output);
            return relativePath;
        }

        public void DeleteFile(string relativePath)
        {
            string path = GetPhysicalPath(relativePath);
            if (File.Exists(path)) File.Delete(path);
            DeletePatientFolderIfEmpty(Path.GetDirectoryName(relativePath));
        }

        public void DeletePatientFolderIfEmpty(string? nationalCode)
        {
            if (string.IsNullOrWhiteSpace(nationalCode)) return;
            string folder = GetPhysicalPath(nationalCode);
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }

        // Used by NationalCode correction / Merge. Caller chooses collision-safe serials.
        public string MoveImageToPatient(string oldRelativePath, string targetNationalCode,
            DateTime uploadDate, int serial)
        {
            ValidateNationalCode(targetNationalCode);
            string extension = Path.GetExtension(oldRelativePath).ToLowerInvariant();
            var pc = new PersianCalendar();
            string date = $"{pc.GetYear(uploadDate):0000}{pc.GetMonth(uploadDate):00}{pc.GetDayOfMonth(uploadDate):00}";
            string fileName = $"{targetNationalCode}_{date}_{serial:000}{extension}";
            string newRelativePath = Path.Combine(targetNationalCode, fileName);
            string oldPath = GetPhysicalPath(oldRelativePath);
            string newPath = GetPhysicalPath(newRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            if (File.Exists(newPath)) throw new IOException($"Destination file exists: {fileName}");
            File.Move(oldPath, newPath);
            DeletePatientFolderIfEmpty(Path.GetDirectoryName(oldRelativePath));
            return newRelativePath;
        }

        // Accepts a safe relative path below RootPath, including NationalCode\FileName.
        public string GetPhysicalPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("RelativePath is required.");
            if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("An absolute path is not allowed.");

            string fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
            string root = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, _rootPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Resolved path is outside the storage root.");
            return fullPath;
        }

        private static void ValidateNationalCode(string nationalCode)
        {
            if (string.IsNullOrWhiteSpace(nationalCode) || !Regex.IsMatch(nationalCode, @"^\d+$"))
                throw new ArgumentException("NationalCode must contain only digits.", nameof(nationalCode));
        }
    }
}
