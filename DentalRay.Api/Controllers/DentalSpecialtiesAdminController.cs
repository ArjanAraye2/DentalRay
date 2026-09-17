using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/admin/specialties")]
    [SuperAdminOnly]
    public class DentalSpecialtiesAdminController : ControllerBase
    {
        private readonly DentalRayDbContext _context;

        public DentalSpecialtiesAdminController(DentalRayDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.DentalSpecialties.AsNoTracking()
                .OrderBy(x => x.SpecialtyName)
                .ToListAsync();
            return Ok(new { success = true, count = items.Count, specialties = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create(DentalSpecialty request)
        {
            string name = (request.SpecialtyName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "نام تخصص الزامی است.", messageEn = "Specialty name is required." });

            if (await _context.DentalSpecialties.AnyAsync(x => x.SpecialtyName == name))
                return Conflict(new { success = false, message = "این تخصص قبلاً ثبت شده است.", messageEn = "This specialty already exists." });

            var item = new DentalSpecialty { SpecialtyName = name, IsActive = true };
            _context.DentalSpecialties.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, specialty = item });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, DentalSpecialty request)
        {
            var item = await _context.DentalSpecialties.FirstOrDefaultAsync(x => x.SpecialtyID == id);
            if (item == null)
                return NotFound(new { success = false, message = "تخصص پیدا نشد.", messageEn = "Specialty not found." });

            string name = (request.SpecialtyName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "نام تخصص الزامی است.", messageEn = "Specialty name is required." });

            if (await _context.DentalSpecialties.AnyAsync(x => x.SpecialtyID != id && x.SpecialtyName == name))
                return Conflict(new { success = false, message = "تخصص دیگری با این نام وجود دارد.", messageEn = "Another specialty with this name already exists." });

            item.SpecialtyName = name;
            item.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, specialty = item });
        }

        [HttpPatch("{id:int}/active")]
        public async Task<IActionResult> SetActive(int id, [FromBody] SetSpecialtyActiveRequest request)
        {
            var item = await _context.DentalSpecialties.FirstOrDefaultAsync(x => x.SpecialtyID == id);
            if (item == null)
                return NotFound(new { success = false, message = "تخصص پیدا نشد.", messageEn = "Specialty not found." });

            item.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, specialty = item });
        }

        public sealed class SetSpecialtyActiveRequest
        {
            public bool IsActive { get; set; }
        }
    }
}
