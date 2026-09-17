using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/admin/studytypes")]
    [SuperAdminOnly]
    public class StudyTypesAdminController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        public StudyTypesAdminController(DentalRayDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.StudyTypes.AsNoTracking()
                .OrderBy(x => x.StudyTypeName).ToListAsync();
            return Ok(new { success = true, count = items.Count, studyTypes = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create(StudyType request)
        {
            string name = (request.StudyTypeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "نام نوع Study الزامی است.", messageEn = "Study Type name is required." });
            if (await _context.StudyTypes.AnyAsync(x => x.StudyTypeName == name))
                return Conflict(new { success = false, message = "این نوع Study قبلاً ثبت شده است.", messageEn = "This Study Type already exists." });

            var item = new StudyType { StudyTypeName = name, IsActive = true };
            _context.StudyTypes.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, studyType = item });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, StudyType request)
        {
            var item = await _context.StudyTypes.FirstOrDefaultAsync(x => x.StudyTypeID == id);
            if (item == null)
                return NotFound(new { success = false, message = "نوع Study پیدا نشد.", messageEn = "Study Type not found." });

            string name = (request.StudyTypeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, message = "نام نوع Study الزامی است.", messageEn = "Study Type name is required." });
            if (await _context.StudyTypes.AnyAsync(x => x.StudyTypeID != id && x.StudyTypeName == name))
                return Conflict(new { success = false, message = "نوع Study دیگری با این نام وجود دارد.", messageEn = "Another Study Type with this name already exists." });

            item.StudyTypeName = name;
            item.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, studyType = item });
        }

        [HttpPatch("{id:int}/active")]
        public async Task<IActionResult> SetActive(int id, [FromBody] SetStudyTypeActiveRequest request)
        {
            var item = await _context.StudyTypes.FirstOrDefaultAsync(x => x.StudyTypeID == id);
            if (item == null)
                return NotFound(new { success = false, message = "نوع Study پیدا نشد.", messageEn = "Study Type not found." });

            item.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, studyType = item });
        }

        public sealed class SetStudyTypeActiveRequest { public bool IsActive { get; set; } }
    }
}
