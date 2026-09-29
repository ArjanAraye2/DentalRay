using ReSiRai.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers
{
    [ApiController]
    [Route("api/studytypes")]
    public class StudyTypesController : ControllerBase
    {
        private readonly ReSiRaiDbContext _context;

        public StudyTypesController(ReSiRaiDbContext context)
        {
            _context = context;
        }

        // Returns active Study types for dropdown/combobox controls.
        // The database remains the single source of truth for these values.
        [HttpGet]
        public async Task<IActionResult> GetActiveStudyTypes()
        {
            var items = await _context.StudyTypes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.StudyTypeName)
                .Select(x => new
                {
                    x.StudyTypeID,
                    x.StudyTypeName
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = items.Count,
                studyTypes = items
            });
        }
    }
}
