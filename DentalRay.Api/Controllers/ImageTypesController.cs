using DentalRay.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/imagetypes")]
    public class ImageTypesController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        public ImageTypesController(DentalRayDbContext context) => _context=context;

        // Operational forms may read active values. Management remains SuperAdmin-only.
        [HttpGet]
        public async Task<IActionResult> GetActive()
        {
            var items=await _context.ImageTypes.AsNoTracking().Where(x=>x.IsActive)
                .OrderBy(x=>x.ImageTypeName)
                .Select(x=>new { x.ImageTypeID,x.ImageTypeName }).ToListAsync();
            return Ok(new { success=true,count=items.Count,imageTypes=items });
        }
    }
}