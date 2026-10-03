using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using ReSiRai.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers
{
    // دیکشنری بیمه‌ها: یک جدول برای هر دو نوع پایه و تکمیلی. نگهداری‌اش فقط در
    // اختیار مدیر سیستم است. حذف فیزیکی نداریم؛ نوعِ در حال استفاده غیرفعال
    // می‌شود تا ارجاعِ پرونده‌های قبلی سالم بماند.
    [ApiController]
    [Route("api/admin/insurances")]
    [SuperAdminOnly]
    public class InsuranceTypesAdminController : ControllerBase
    {
        private readonly ReSiRaiDbContext _context;
        public InsuranceTypesAdminController(ReSiRaiDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // ترتیب الفبایی؛ ابتدا پایه‌ها سپس تکمیلی‌ها تا فهرست ادمین هم خوانا باشد.
            var items = await _context.InsuranceTypes.AsNoTracking()
                .OrderBy(x => x.IsSupplementary).ThenBy(x => x.InsuranceTypeName)
                .ToListAsync();
            return Ok(new { success = true, count = items.Count, insurances = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create(InsuranceType request)
        {
            string name = (request.InsuranceTypeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "نام بیمه الزامی است.", messageEn = "Insurance name is required." });

            bool kind = request.IsSupplementary;
            if (await _context.InsuranceTypes.AnyAsync(x => x.InsuranceTypeName == name && x.IsSupplementary == kind))
                return Conflict(new { success = false, message = "این بیمه قبلاً ثبت شده است.", messageEn = "This insurance already exists." });

            var item = new InsuranceType { InsuranceTypeName = name, IsSupplementary = kind, IsActive = true };
            _context.InsuranceTypes.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, insurance = item });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, InsuranceType request)
        {
            var item = await _context.InsuranceTypes.FirstOrDefaultAsync(x => x.InsuranceTypeID == id);
            if (item == null)
                return NotFound(new { success = false, message = "بیمه پیدا نشد.", messageEn = "Insurance not found." });

            string name = (request.InsuranceTypeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "نام بیمه الزامی است.", messageEn = "Insurance name is required." });

            bool kind = request.IsSupplementary;
            if (await _context.InsuranceTypes.AnyAsync(x => x.InsuranceTypeID != id && x.InsuranceTypeName == name && x.IsSupplementary == kind))
                return Conflict(new { success = false, message = "بیمهٔ دیگری با این نام وجود دارد.", messageEn = "Another insurance with this name already exists." });

            item.InsuranceTypeName = name;
            item.IsSupplementary = kind;
            item.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, insurance = item });
        }

        [HttpPatch("{id:int}/active")]
        public async Task<IActionResult> SetActive(int id, [FromBody] SetInsuranceActiveRequest request)
        {
            var item = await _context.InsuranceTypes.FirstOrDefaultAsync(x => x.InsuranceTypeID == id);
            if (item == null)
                return NotFound(new { success = false, message = "بیمه پیدا نشد.", messageEn = "Insurance not found." });

            item.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, insurance = item });
        }

        public sealed class SetInsuranceActiveRequest { public bool IsActive { get; set; } }
    }
}
