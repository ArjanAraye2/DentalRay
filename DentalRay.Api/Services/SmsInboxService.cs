using System.Text.RegularExpressions;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Services
{
    // ============================================================
    // Reads the radiology SMS that arrived on a paired phone.
    //
    // The text of an SMS is the only thing we get, so the matcher works from
    // the strongest signal to the weakest:
    //   1. a share link inside the text  -> the token names the patient exactly
    //   2. a 10 digit national code
    //   3. the sender's mobile number
    //   4. the patient's name, compared with Persian letters normalised
    // Anything less than a sure match stays pending for the secretary - a
    // guessed patient would put X-rays on the wrong record.
    // ============================================================
    public class SmsInboxService
    {
        public const byte MethodLink = 1;
        public const byte MethodNationalCode = 2;
        public const byte MethodSender = 3;
        public const byte MethodName = 4;

        private readonly DentalRayDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly RadiologyStorageService _storage;
        private readonly ILogger<SmsInboxService> _logger;

        public SmsInboxService(DentalRayDbContext db, IHttpClientFactory http, RadiologyStorageService storage, ILogger<SmsInboxService> logger)
        {
            _db = db;
            _http = http;
            _storage = storage;
            _logger = logger;
        }

        // ------------------------------------------------------------
        // Text helpers
        // ------------------------------------------------------------
        private static readonly Regex LinkPattern = new(
            @"https?://[^\s<>""']+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DigitsPattern = new(@"\d{10}", RegexOptions.Compiled);

        /// <summary>All http(s) links inside a message, in order, without repeats.</summary>
        public IReadOnlyList<string> ExtractLinks(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<string>();
            var found = new List<string>();
            foreach (Match match in LinkPattern.Matches(body))
            {
                string url = match.Value.TrimEnd('.', ',', ';', ')', ']', '،');
                if (!found.Contains(url, StringComparer.OrdinalIgnoreCase)) found.Add(url);
            }
            return found;
        }

        /// <summary>Latin digits, Arabic/Persian letters unified, decoration removed.</summary>
        public static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var sb = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                char mapped = c switch
                {
                    '\u064A' => 'ی',   // Arabic yeh
                    '\u0649' => 'ی',   // alef maksura
                    '\u0643' => 'ک',   // Arabic kaf
                    '\u0640' => '\0',   // tatweel
                    '\u200C' => '\0',   // zero width non-joiner
                    '\u200D' => '\0',
                    >= '\u064B' and <= '\u0652' => '\0', // harakat
                    >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')), // Persian digits
                    >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')), // Arabic digits
                    _ => c
                };
                if (mapped != '\0') sb.Append(mapped);
            }
            return sb.ToString();
        }

        /// <summary>Only letters/digits, lower cased - used to compare names.</summary>
        public static string NameKey(string? value) =>
            string.Concat(NormalizeText(value).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));

        /// <summary>09123456789 and 98123456789 end up equal.</summary>
        public static string NormalizeMobile(string? value)
        {
            string digits = string.Concat(NormalizeText(value).Where(char.IsDigit));
            if (digits.Length is 13 or 12 && digits.StartsWith("98")) digits = "0" + digits[2..];
            else if (digits.Length == 10) digits = "0" + digits;
            return digits;
        }

        // ------------------------------------------------------------
        // Matching
        // ------------------------------------------------------------
        public sealed record MatchResult(int? PatientID, byte? Method, string? Note);

        public async Task<MatchResult> MatchAsync(string body, string? senderMobile, CancellationToken cancellationToken = default)
        {
            // 1. a share link identifies the patient beyond doubt
            foreach (string link in ExtractLinks(body))
            {
                string? token = TokenFromLink(link);
                if (token == null) continue;
                int? patientID = await (
                    from share in _db.StudyShareLinks.AsNoTracking()
                    join study in _db.RadiologyStudies.AsNoTracking() on share.StudyID equals study.StudyID
                    where share.Token == token
                    select (int?)study.PatientID).FirstOrDefaultAsync(cancellationToken);
                if (patientID.HasValue)
                    return new MatchResult(patientID.Value, MethodLink, null);
            }

            string text = NormalizeText(body);

            // 2. national code written inside the message
            foreach (Match code in DigitsPattern.Matches(text))
            {
                var patient = await _db.Patients.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.NationalCode == code.Value, cancellationToken);
                if (patient != null) return new MatchResult(patient.PatientID, MethodNationalCode, null);
            }

            // 3. the sender's own number
            string sender = NormalizeMobile(senderMobile);
            if (sender.Length >= 10)
            {
                var patient = await _db.Patients.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Mobile != null && p.Mobile == sender, cancellationToken);
                if (patient != null) return new MatchResult(patient.PatientID, MethodSender, null);
            }

            // 4. the name, normalised. Loaded once: the table is clinic sized.
            string key = NameKey(text);
            if (key.Length < 5) return new MatchResult(null, null, "پیامک فاقد لینک، کد ملی، شماره یا نام خوانا است.");

            var candidates = await _db.Patients.AsNoTracking()
                .Where(p => p.IsActive)
                .Select(p => new { p.PatientID, p.FirstName, p.LastName, p.NationalCode, p.Mobile })
                .ToListAsync(cancellationToken);

            var hits = candidates
                .Where(p =>
                {
                    string full = NameKey(p.FirstName + " " + p.LastName);
                    string last = NameKey(p.LastName);
                    return full.Length > 0 && key.Contains(full)
                        || last.Length >= 4 && key.Contains(last);
                })
                .ToList();

            if (hits.Count == 1) return new MatchResult(hits[0].PatientID, MethodName, null);
            if (hits.Count > 1)
                return new MatchResult(null, null, $"چند بیمار با این نام پیدا شدند ({hits.Count} نفر)؛ لطفاً دستی انتخاب کنید.");
            return new MatchResult(null, null, "بیماری با این مشخصات در دیتابیس نیست.");
        }

        /// <summary>/s/{token} or /api/shared/{token} -> the token</summary>
        public static string? TokenFromLink(string link)
        {
            var match = Regex.Match(link, @"/(?:s|api/shared)/([A-Za-z0-9_\-]{16,64})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        // ------------------------------------------------------------
        // Importing the pictures a link points at
        // ------------------------------------------------------------
        public sealed record FetchedImage(byte[] Content, string FileName, string ContentType, string? ImageTypeName);

        /// <summary>
        /// Downloads every image behind one share link. The link is our own
        /// viewer, so this walks its public API - no login, no patient secret.
        /// </summary>
        public async Task<IReadOnlyList<FetchedImage>> FetchSharedImagesAsync(string shareLink, CancellationToken cancellationToken = default)
        {
            string? token = TokenFromLink(shareLink);
            if (token == null) return Array.Empty<FetchedImage>();

            Uri? baseUri = Uri.TryCreate(shareLink, UriKind.Absolute, out var parsed) ? parsed : null;
            if (baseUri == null) return Array.Empty<FetchedImage>();
            string origin = $"{baseUri.Scheme}://{baseUri.Authority}";

            var client = _http.CreateClient("ShareImages");
            client.Timeout = TimeSpan.FromSeconds(60);

            var results = new List<FetchedImage>();

            // Our own viewer answers with JSON. Another radiologist's system
            // will not, so a failed parse simply falls through to the page below.
            try
            {
                string metadataJson = await client.GetStringAsync($"{origin}/api/shared/{token}", cancellationToken);
                var metadata = System.Text.Json.JsonDocument.Parse(metadataJson);
                if (metadata.RootElement.TryGetProperty("success", out var ok) && ok.GetBoolean())
                {
                    foreach (var image in metadata.RootElement.GetProperty("images").EnumerateArray())
                    {
                        if (image.TryGetProperty("isPdf", out var pdf) && pdf.GetBoolean()) continue;
                        long imageID = image.GetProperty("imageID").GetInt64();
                        string url = image.GetProperty("url").GetString() ?? "";
                        // The public answer deliberately leaves the file name out -
                        // our names carry the patient's national code. Fall back
                        // to a neutral name built from the image id.
                        string fileName = image.TryGetProperty("fileName", out var fn)
                            && fn.ValueKind == System.Text.Json.JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(fn.GetString())
                                ? fn.GetString()!
                                : $"image-{imageID}.jpg";
                        string? typeName = image.TryGetProperty("imageTypeName", out var tn) && tn.ValueKind == System.Text.Json.JsonValueKind.String
                            ? tn.GetString() : null;
                        byte[] bytes = await client.GetByteArrayAsync($"{origin}{url}", cancellationToken);
                        if (bytes.Length == 0) continue;
                        results.Add(new FetchedImage(bytes, fileName, "image/jpeg", typeName));
                    }
                    if (results.Count > 0) return results;
                }
            }
            catch (Exception ex)
            {
                // Not our JSON - and worth saying why, because "no images"
                // usually means this call failed rather than the link being empty.
                _logger.LogWarning(ex, "Share metadata could not be read from {Origin}", origin);
            }

            // A page written by someone else: take the pictures it displays,
            // skipping logos and icons. Nothing else can be trusted blindly.
            try
            {
                string html = await client.GetStringAsync(shareLink, cancellationToken);
                foreach (Match match in Regex.Matches(html, "<img[^>]+src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase))
                {
                    if (!Uri.TryCreate(baseUri, match.Groups[1].Value, out var imageUri)) continue;
                    string path = imageUri.AbsolutePath.ToLowerInvariant();
                    if (path.EndsWith(".svg") || path.Contains("logo") || path.Contains("icon") || path.Contains("favicon")) continue;
                    byte[] bytes;
                    try { bytes = await client.GetByteArrayAsync(imageUri, cancellationToken); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Picture {Url} could not be downloaded", imageUri); continue; }
                    if (bytes.Length < 1500) continue;
                    results.Add(new FetchedImage(bytes, Path.GetFileName(imageUri.AbsolutePath), "image/jpeg", null));
                    if (results.Count >= 20) break;
                }
            }
            catch (Exception ex)
            {
                // the page could not be read either; the caller reports nothing found
                _logger.LogWarning(ex, "Share page {Url} could not be read", shareLink);
            }

            _logger.LogInformation("Share {Url} produced {Count} picture(s)", shareLink, results.Count);
            return results;
        }

        // ------------------------------------------------------------
        // Saving what was fetched into the patient's own images
        // ------------------------------------------------------------
        /// <summary>
        /// Stores the fetched files as patient-owned images and shows them in a
        /// Study: the one the caller names (the secretary opened it), or the
        /// patient's newest Study when none was named. A file already stored
        /// for this patient is attached again instead of being copied, so
        /// repeated SMS messages never duplicate a picture.
        /// </summary>
        public sealed record ImportResult(int Imported, int? StudyID);

        public async Task<ImportResult> ImportToPatientAsync(
            int patientID,
            IReadOnlyList<FetchedImage> files,
            int? studyID = null,
            CancellationToken cancellationToken = default)
        {
            if (files.Count == 0) return new ImportResult(0, null);
            var patient = await _db.Patients.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PatientID == patientID, cancellationToken);
            if (patient == null) return new ImportResult(0, null);

            int serial = await _db.RadiologyImages.Where(x => x.PatientID == patientID)
                .Select(x => (int?)x.SerialNumber).MaxAsync(cancellationToken) ?? 0;

            var touched = new List<long>();
            int imported = 0;

            foreach (var file in files)
            {
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file.Content)).ToLowerInvariant();
                var existing = await _db.RadiologyImages.AsNoTracking()
                    .Where(x => x.PatientID == patientID && x.ContentHash == hash)
                    .Select(x => (long?)x.ImageID)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing.HasValue)
                {
                    touched.Add(existing.Value);
                    continue;
                }

                serial++;
                string relativePath;
                try
                {
                    await using var memory = new MemoryStream(file.Content);
                    relativePath = await _storage.SaveImageAsync(memory, file.FileName, patient.NationalCode, DateTime.Now, serial);
                }
                catch { continue; }

                int? imageTypeID = null;
                if (!string.IsNullOrWhiteSpace(file.ImageTypeName))
                {
                    imageTypeID = await _db.ImageTypes.AsNoTracking()
                        .Where(x => x.IsActive && x.ImageTypeName == file.ImageTypeName)
                        .Select(x => (int?)x.ImageTypeID)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                var image = new RadiologyImage
                {
                    PatientID = patientID,
                    ImageTypeID = imageTypeID,
                    FileName = Path.GetFileName(relativePath),
                    RelativePath = relativePath,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType,
                    SerialNumber = serial,
                    CreatedDate = DateTime.Now,
                    ContentHash = hash
                };
                _db.RadiologyImages.Add(image);
                await _db.SaveChangesAsync(cancellationToken);
                touched.Add(image.ImageID);
                imported++;
            }

            if (touched.Count == 0) return new ImportResult(0, null);

            // Study مقصد: آنچه منشی باز کرده، وگرنه آخرین Study بیمار
            int? targetStudy = studyID;
            if (targetStudy.HasValue)
            {
                bool belongs = await _db.RadiologyStudies.AsNoTracking()
                    .AnyAsync(x => x.StudyID == targetStudy.Value && x.PatientID == patientID, cancellationToken);
                if (!belongs) targetStudy = null;
            }
            if (!targetStudy.HasValue)
            {
                targetStudy = await _db.RadiologyStudies.AsNoTracking()
                    .Where(x => x.PatientID == patientID)
                    .OrderByDescending(x => x.StudyDate)
                    .ThenByDescending(x => x.StudyID)
                    .Select(x => (int?)x.StudyID)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (targetStudy.HasValue)
            {
                foreach (long imageID in touched)
                {
                    bool linked = await _db.RadiologyStudyImages.AsNoTracking()
                        .AnyAsync(x => x.StudyID == targetStudy.Value && x.ImageID == imageID, cancellationToken);
                    if (linked) continue;
                    _db.RadiologyStudyImages.Add(new RadiologyStudyImage
                    {
                        StudyID = targetStudy.Value,
                        ImageID = imageID,
                        CreatedDate = DateTime.Now
                    });
                }
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new ImportResult(imported, targetStudy);
        }
    }
}
