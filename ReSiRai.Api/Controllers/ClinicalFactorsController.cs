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
        /// its sections from this one call.
        /// </summary>
        [HttpGet("definitions")]
        public async Task<IActionResult> Definitions([FromQuery] int specialtyID, [FromQuery] string? category = null)
        {
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
                    x.f.FactorID, x.f.FactorCode, x.f.NameFa, x.f.NameEn,
                    x.f.Category, x.f.DataType, x.f.UnitUCUM,
                    x.f.LoincCode, x.f.LoincStatus,
                    x.f.RefLow, x.f.RefHigh, x.f.RefText, x.f.RefSource, x.f.RefPopulation,
                    x.f.AbnormalDirection, x.f.OptionsJson,
                    x.s.IsRequired, x.s.IsCommon, x.s.SortOrder
                })
                .ToListAsync();

            return Ok(new { success = true, count = rows.Count, factors = rows });
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
