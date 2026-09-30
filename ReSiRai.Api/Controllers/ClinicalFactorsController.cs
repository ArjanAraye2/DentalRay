using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers
{
    // Clinical decision factors ("sharayet-e feli"): what the patient's condition
    // is, as numbers the AI consultation can reason over.
    //
    // Reading is open to any signed-in user because the visit form needs the factor
    // definitions and the recorded values. Recording values is normal staff work;
    // changing the dictionary itself stays in the factor admin (later phase).
    //
    // Every value row says where its number came from (manual, lab-report
    // extraction, device, computed) so an AI input is always auditable.
    [ApiController]
    [Route("api/factors")]
    public class ClinicalFactorsController : ControllerBase
    {
        private readonly ReSiRaiDbContext _db;
        public ClinicalFactorsController(ReSiRaiDbContext db) => _db = db;

        public sealed class FactorValueItem
        {
            public int FactorID { get; set; }
            public decimal? ValueNumber { get; set; }
            public string? ValueText { get; set; }
            public bool? ValueBit { get; set; }
            public DateTime? ValueDate { get; set; }
            /// <summary>When the measurement was taken; defaults to now.</summary>
            public DateTime? ObservedAt { get; set; }
            /// <summary>1 = manual, 2 = lab-report extraction, 3 = device, 4 = computed.</summary>
            public byte Source { get; set; } = 1;
            public long? ExtractionID { get; set; }
            public decimal? Confidence { get; set; }
        }

        public sealed class SaveValuesRequest
        {
            public int StudyID { get; set; }
            public List<FactorValueItem> Items { get; set; } = new();
        }

        /// <summary>
        /// The factor set of one specialty: shared dictionary rows joined with the
        /// specialty binding (required / always shown / order). The visit form builds
        /// its sections from this one call. Callers may pass a visit instead of a
        /// specialty; the specialty is then taken from the visit's doctor, falling
        /// back to internal medicine when the doctor has none.
        /// </summary>
        [HttpGet("definitions")]
        public async Task<IActionResult> Definitions([FromQuery] int? specialtyID = null,
            [FromQuery] int? studyID = null, [FromQuery] string? category = null)
        {
            if (!specialtyID.HasValue && studyID.HasValue)
            {
                var staffID = await _db.RadiologyStudies.AsNoTracking()
                    .Where(x => x.StudyID == studyID.Value)
                    .Select(x => x.DoctorStaffID)
                    .FirstOrDefaultAsync();
                specialtyID = await _db.Staff.AsNoTracking()
                    .Where(x => x.StaffID == staffID)
                    .Select(x => x.SpecialtyID)
                    .FirstOrDefaultAsync();
            }
            if (!specialtyID.HasValue)
            {
                // Phase 1 starts from internal medicine; a doctor without a specialty
                // therefore sees the internal-medicine factor set.
                specialtyID = await _db.Specialties.AsNoTracking()
                    .Where(x => x.SpecialtyName == "بیماری‌های داخلی")
                    .Select(x => x.SpecialtyID)
                    .FirstOrDefaultAsync();
            }

            var query =
                from f in _db.ClinicalFactors.AsNoTracking()
                join s in _db.SpecialtyFactorSets.AsNoTracking() on f.FactorID equals s.FactorID
                where s.SpecialtyID == specialtyID && f.IsActive
                select new { f, s };

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(x => x.f.Category == category);

            var rows = await query
                .OrderByDescending(x => x.s.IsCommon).ThenBy(x => x.s.SortOrder).ThenBy(x => x.f.FactorID)
                .Select(x => new
                {
                    x.f.FactorID, x.f.FactorCode, x.f.NameFa, x.f.NameEn, x.f.ShortCode,
                    x.f.Category, x.f.DataType, x.f.UnitUCUM,
                    x.f.LoincCode, x.f.LoincStatus,
                    x.f.RefLow, x.f.RefHigh, x.f.RefText, x.f.RefSource, x.f.RefPopulation,
                    x.f.AbnormalDirection, x.f.OptionsJson,
                    x.s.IsRequired, x.s.IsCommon, x.s.SortOrder
                })
                .ToListAsync();

            return Ok(new { success = true, count = rows.Count, factors = rows });
        }

        public sealed class AddFactorRequest
        {
            public string? NameFa { get; set; }
            public string NameEn { get; set; } = string.Empty;
            public string? ShortCode { get; set; }
            public string? UnitUCUM { get; set; }
            public decimal? RefLow { get; set; }
            public decimal? RefHigh { get; set; }
            public string? RefText { get; set; }
            /// <summary>1 = number, 4 = text; unknown printed tests default to number.</summary>
            public byte DataType { get; set; } = 1;
            public int? StudyID { get; set; }
        }

        /// <summary>
        /// Adds a test the lab printed but the dictionary did not know yet, so no
        /// printed result is ever thrown away for being unknown. The row starts as
        /// "pending specialist review" with no LOINC code (a code is never guessed)
        /// and is bound to the visit's specialty so the panel shows it at once.
        /// </summary>
        [HttpPost("definitions")]
        public async Task<IActionResult> AddDefinition(AddFactorRequest request, CancellationToken cancellationToken)
        {
            string nameEn = (request.NameEn ?? string.Empty).Trim();
            string nameFa = string.IsNullOrWhiteSpace(request.NameFa) ? nameEn : request.NameFa.Trim();
            if (nameEn.Length == 0 && nameFa.Length == 0)
                return BadRequest(new { success = false, message = "نام تست لازم است." });
            if (nameEn.Length == 0) nameEn = nameFa;

            // The same printed test must never create two dictionary rows.
            string enLower = nameEn.ToLowerInvariant();
            string faKey = nameFa.ToLowerInvariant();
            var factor = await _db.ClinicalFactors
                .FirstOrDefaultAsync(x => x.IsActive &&
                    (x.NameEn.ToLower() == enLower || x.NameFa.ToLower() == faKey), cancellationToken);
            bool created = false;
            if (factor is null)
            {
                factor = new ClinicalFactor
                {
                    FactorCode = await UniqueFactorCode(nameEn, cancellationToken),
                    NameFa = nameFa,
                    NameEn = nameEn,
                    ShortCode = string.IsNullOrWhiteSpace(request.ShortCode) ? null : request.ShortCode.Trim(),
                    Category = "Lab",
                    DataType = request.DataType == 0 ? (byte)1 : request.DataType,
                    UnitUCUM = string.IsNullOrWhiteSpace(request.UnitUCUM) ? null : request.UnitUCUM.Trim(),
                    LoincCode = null,
                    LoincStatus = 0,
                    RefLow = request.RefLow,
                    RefHigh = request.RefHigh,
                    RefText = string.IsNullOrWhiteSpace(request.RefText) ? null : request.RefText.Trim(),
                    RefSource = "برگه آزمایش چاپ‌شده (ثبت هنگام استخراج)",
                    RefPopulation = null,
                    AbnormalDirection = request.RefLow.HasValue || request.RefHigh.HasValue ? (byte)3 : null,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };
                _db.ClinicalFactors.Add(factor);
                await _db.SaveChangesAsync(cancellationToken);
                created = true;
            }

            // Bind it to the visit's specialty (or internal medicine) so the factor
            // set of that specialty can serve it to the panel right away.
            int specialtyID = 0;
            if (request.StudyID.HasValue)
            {
                var staffID = await _db.RadiologyStudies.AsNoTracking()
                    .Where(x => x.StudyID == request.StudyID.Value)
                    .Select(x => x.DoctorStaffID)
                    .FirstOrDefaultAsync(cancellationToken);
                specialtyID = (await _db.Staff.AsNoTracking()
                    .Where(x => x.StaffID == staffID)
                    .Select(x => x.SpecialtyID)
                    .FirstOrDefaultAsync(cancellationToken)) ?? 0;
            }
            if (specialtyID == 0)
            {
                specialtyID = await _db.Specialties.AsNoTracking()
                    .Where(x => x.SpecialtyName == "بیماری‌های داخلی")
                    .Select(x => x.SpecialtyID)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            bool bound = false;
            if (specialtyID != 0 && !await _db.SpecialtyFactorSets.AsNoTracking()
                .AnyAsync(x => x.SpecialtyID == specialtyID && x.FactorID == factor.FactorID, cancellationToken))
            {
                int maxOrder = await _db.SpecialtyFactorSets.AsNoTracking()
                    .Where(x => x.SpecialtyID == specialtyID)
                    .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;
                _db.SpecialtyFactorSets.Add(new SpecialtyFactorSet
                {
                    SpecialtyID = specialtyID,
                    FactorID = factor.FactorID,
                    IsRequired = false,
                    IsCommon = false,
                    SortOrder = maxOrder + 1
                });
                await _db.SaveChangesAsync(cancellationToken);
                bound = true;
            }

            return Ok(new
            {
                success = true,
                created,
                bound,
                factor = new
                {
                    factorID = factor.FactorID,
                    factorCode = factor.FactorCode,
                    nameFa = factor.NameFa,
                    nameEn = factor.NameEn,
                    shortCode = factor.ShortCode,
                    unitUCUM = factor.UnitUCUM,
                    refLow = factor.RefLow,
                    refHigh = factor.RefHigh,
                    refText = factor.RefText,
                    dataType = factor.DataType,
                    loincStatus = factor.LoincStatus
                }
            });
        }

        /// <summary>Builds a readable, unique internal code for a new dictionary row.</summary>
        private async Task<string> UniqueFactorCode(string nameEn, CancellationToken cancellationToken)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in nameEn.ToLowerInvariant())
                if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(c);
            string slug = sb.Length >= 2 ? sb.ToString()[..Math.Min(sb.Length, 24)] : "test";
            string code = $"LAB.CUSTOM.{slug}";
            for (int i = 2; await _db.ClinicalFactors.AsNoTracking()
                .AnyAsync(x => x.FactorCode == code, cancellationToken); i++)
                code = $"LAB.CUSTOM.{slug}-{i}";
            return code;
        }

        /// <summary>
        /// All recorded values of one visit, with the factor identity attached, plus
        /// the earlier values of the same factors so the form can draw a trend.
        /// </summary>
        [HttpGet("values")]
        public async Task<IActionResult> Values([FromQuery] int studyID)
        {
            var rows = await (
                from v in _db.StudyFactorValues.AsNoTracking()
                join f in _db.ClinicalFactors.AsNoTracking() on v.FactorID equals f.FactorID
                where v.StudyID == studyID
                orderby v.ObservedAt descending
                select new
                {
                    v.FactorValueID, v.StudyID, v.FactorID,
                    v.ValueNumber, v.ValueText, v.ValueBit, v.ValueDate,
                    v.ObservedAt, v.Source, v.ExtractionID, v.Confidence,
                    f.FactorCode, f.NameFa, f.NameEn, f.DataType, f.UnitUCUM,
                    f.RefLow, f.RefHigh, f.RefText, f.AbnormalDirection, f.Category
                }).ToListAsync();

            return Ok(new { success = true, count = rows.Count, values = rows });
        }

        /// <summary>
        /// Record or update values in one visit. A row that matches the visit, the
        /// factor and the measurement time is updated instead of duplicated, so the
        /// review screen can keep editing what it shows.
        /// </summary>
        [HttpPost("values")]
        public async Task<IActionResult> Save(SaveValuesRequest request)
        {
            if (request.StudyID <= 0) return BadRequest(new { success = false, message = "مراجعه مشخص نیست." });
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { success = false, message = "هیچ مقداری برای ثبت ارسال نشده است." });

            bool studyExists = await _db.RadiologyStudies.AsNoTracking().AnyAsync(x => x.StudyID == request.StudyID);
            if (!studyExists) return NotFound(new { success = false, message = "مراجعه پیدا نشد." });

            var factorIds = request.Items.Select(x => x.FactorID).Distinct().ToList();
            var factors = await _db.ClinicalFactors.AsNoTracking()
                .Where(x => factorIds.Contains(x.FactorID) && x.IsActive)
                .ToListAsync();
            if (factors.Count != factorIds.Count)
                return BadRequest(new { success = false, message = "یکی از فاکتورها ناشناخته یا غیرفعال است." });

            foreach (var item in request.Items)
            {
                var factor = factors.First(x => x.FactorID == item.FactorID);
                var error = ValidateValue(factor, item);
                if (error != null) return BadRequest(new { success = false, message = error });

                var observedAt = item.ObservedAt ?? DateTime.Now;
                var row = await _db.StudyFactorValues.FirstOrDefaultAsync(x =>
                    x.StudyID == request.StudyID && x.FactorID == item.FactorID && x.ObservedAt == observedAt);

                if (row == null)
                {
                    row = new StudyFactorValue
                    {
                        StudyID = request.StudyID,
                        FactorID = item.FactorID,
                        ObservedAt = observedAt,
                        CreatedDate = DateTime.Now
                    };
                    _db.StudyFactorValues.Add(row);
                }

                row.ValueNumber = factor.DataType == 1 || factor.DataType == 2 ? item.ValueNumber : null;
                row.ValueText = Clean(item.ValueText);
                row.ValueBit = item.ValueBit;
                row.ValueDate = item.ValueDate;
                row.Source = item.Source;
                row.ExtractionID = item.ExtractionID;
                row.Confidence = item.Confidence;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, saved = request.Items.Count });
        }

        /// <summary>Remove one recorded value (used when a review step rejects an extraction).</summary>
        [HttpDelete("values/{id:long}")]
        public async Task<IActionResult> DeleteValue(long id)
        {
            var row = await _db.StudyFactorValues.FirstOrDefaultAsync(x => x.FactorValueID == id);
            if (row == null) return NotFound(new { success = false, message = "مقدار مورد نظر پیدا نشد." });

            _db.StudyFactorValues.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private static string? ValidateValue(ClinicalFactor factor, FactorValueItem item)
        {
            var label = factor.NameFa;
            switch (factor.DataType)
            {
                case 1: // number
                    if (!item.ValueNumber.HasValue)
                        return $"مقدار «{label}» باید عدد باشد.";
                    break;
                case 2: // enum: stored as the option's numeric value
                    if (!item.ValueNumber.HasValue)
                        return $"گزینه‌ای برای «{label}» انتخاب نشده است.";
                    break;
                case 3: // boolean
                    if (!item.ValueBit.HasValue)
                        return $"وضعیت «{label}» مشخص نیست.";
                    break;
                case 4: // text
                    if (string.IsNullOrWhiteSpace(item.ValueText))
                        return $"متن «{label}» خالی است.";
                    break;
                case 5: // date
                    if (!item.ValueDate.HasValue)
                        return $"تاریخ «{label}» وارد نشده است.";
                    break;
            }

            if (factor.DataType != 4 && !string.IsNullOrWhiteSpace(item.ValueText) && item.ValueText.Length > 500)
                return $"متن «{label}» بیش از حد مجاز است.";
            return null;
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
