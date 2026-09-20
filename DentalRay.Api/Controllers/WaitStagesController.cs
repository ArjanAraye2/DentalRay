using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Waiting stages: what a patient is held up by, per specialty.
    //
    // Reading is open to any signed-in user because the study form needs to fill
    // its dropdown. Creating and editing definitions is SuperAdmin work, matching
    // how the other lookups behave.
    [ApiController]
    [Route("api/waitstages")]
    public class WaitStagesController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        public WaitStagesController(DentalRayDbContext db) => _db = db;

        public sealed class WaitStageRequest
        {
            public string Name { get; set; } = string.Empty;
            public int? SpecialtyID { get; set; }
            public string? SmsTemplate { get; set; }
            public int SortOrder { get; set; }
            public bool IsActive { get; set; } = true;
        }

        /// <summary>
        /// Active stages with their specialty name. A caller may pass a specialty to
        /// get the stages for it plus the ones that apply to everyone, which is what
        /// the study form uses to suggest a stage from the chosen dentist.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? specialtyID = null, [FromQuery] bool all = false)
        {
            var query = _db.WaitStages.AsNoTracking().AsQueryable();
            if (!all) query = query.Where(x => x.IsActive);
            if (specialtyID.HasValue)
                query = query.Where(x => x.SpecialtyID == null || x.SpecialtyID == specialtyID.Value);

            var rows = await query
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new
                {
                    x.WaitStageID, x.Name, x.SpecialtyID, x.SmsTemplate,
                    x.SortOrder, x.IsActive,
                    SpecialtyName = _db.DentalSpecialties.AsNoTracking()
                        .Where(s => s.SpecialtyID == x.SpecialtyID)
                        .Select(s => s.SpecialtyName).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new { success = true, count = rows.Count, waitStages = rows });
        }

        [HttpPost]
        [SuperAdminOnly]
        public async Task<IActionResult> Create(WaitStageRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(new { success = false, message = error });

            var row = new WaitStage
            {
                Name = request.Name.Trim(),
                SpecialtyID = request.SpecialtyID,
                SmsTemplate = Clean(request.SmsTemplate),
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                CreatedDate = DateTime.Now
            };
            _db.WaitStages.Add(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, waitStage = row });
        }

        [HttpPut("{id:int}")]
        [SuperAdminOnly]
        public async Task<IActionResult> Update(int id, WaitStageRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(new { success = false, message = error });

            var row = await _db.WaitStages.FirstOrDefaultAsync(x => x.WaitStageID == id);
            if (row == null) return NotFound(new { success = false, message = "مرحله انتظار پیدا نشد." });

            row.Name = request.Name.Trim();
            row.SpecialtyID = request.SpecialtyID;
            row.SmsTemplate = Clean(request.SmsTemplate);
            row.SortOrder = request.SortOrder;
            row.IsActive = request.IsActive;
            row.ModifiedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, waitStage = row });
        }

        [HttpDelete("{id:int}")]
        [SuperAdminOnly]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.WaitStages.FirstOrDefaultAsync(x => x.WaitStageID == id);
            if (row == null) return NotFound(new { success = false, message = "مرحله انتظار پیدا نشد." });

            // A study may still point at this stage, so block the delete instead of
            // silently leaving studies with a dangling reference.
            bool inUse = await _db.RadiologyStudies.AsNoTracking().AnyAsync(s => s.WaitStageID == id);
            if (inUse) return BadRequest(new { success = false, message = "این مرحله در مطالعات استفاده شده و قابل حذف نیست. آن را غیرفعال کنید." });

            _db.WaitStages.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private static string? Validate(WaitStageRequest x)
        {
            if (string.IsNullOrWhiteSpace(x.Name)) return "نام مرحله انتظار را وارد کنید.";
            if (x.Name.Trim().Length > 100) return "نام مرحله انتظار نمی‌تواند بیشتر از ۱۰۰ نویسه باشد.";
            if (x.SmsTemplate is { Length: > 500 }) return "متن پیامک نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.";
            return null;
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
