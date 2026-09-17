using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/admin/imagetypes")]
    [SuperAdminOnly]
    public class ImageTypesAdminController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        public ImageTypesAdminController(DentalRayDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.ImageTypes.AsNoTracking().OrderBy(x => x.ImageTypeName).ToListAsync();
            return Ok(new { success = true, count = items.Count, imageTypes = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create(ImageType request)
        {
            string name = (request.ImageTypeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success=false, message="نام نوع تصویر الزامی است.", messageEn="Image Type name is required." });
            if (await _context.ImageTypes.AnyAsync(x => x.ImageTypeName == name))
                return Conflict(new { success=false, message="این نوع تصویر قبلاً ثبت شده است.", messageEn="This Image Type already exists." });
            var item = new ImageType { ImageTypeName=name, IsActive=true };
            _context.ImageTypes.Add(item); await _context.SaveChangesAsync();
            return Ok(new { success=true, imageType=item });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, ImageType request)
        {
            var item=await _context.ImageTypes.FirstOrDefaultAsync(x=>x.ImageTypeID==id);
            if(item==null) return NotFound(new { success=false,message="نوع تصویر پیدا نشد.",messageEn="Image Type not found." });
            string name=(request.ImageTypeName??string.Empty).Trim();
            if(string.IsNullOrWhiteSpace(name)) return BadRequest(new { success=false,message="نام نوع تصویر الزامی است.",messageEn="Image Type name is required." });
            if(await _context.ImageTypes.AnyAsync(x=>x.ImageTypeID!=id && x.ImageTypeName==name))
                return Conflict(new { success=false,message="نوع تصویر دیگری با این نام وجود دارد.",messageEn="Another Image Type with this name already exists." });
            item.ImageTypeName=name; item.IsActive=request.IsActive; await _context.SaveChangesAsync();
            return Ok(new { success=true,imageType=item });
        }

        [HttpPatch("{id:int}/active")]
        public async Task<IActionResult> SetActive(int id,[FromBody] SetImageTypeActiveRequest request)
        {
            var item=await _context.ImageTypes.FirstOrDefaultAsync(x=>x.ImageTypeID==id);
            if(item==null) return NotFound(new { success=false,message="نوع تصویر پیدا نشد.",messageEn="Image Type not found." });
            item.IsActive=request.IsActive; await _context.SaveChangesAsync();
            return Ok(new { success=true,imageType=item });
        }

        public sealed class SetImageTypeActiveRequest { public bool IsActive { get; set; } }
    }
}