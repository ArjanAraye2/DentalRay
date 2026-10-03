using ReSiRai.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers
{
    // خواندن دیکشنری بیمه برای کشوهای فرم بیمار. نگهداری فقط در کنترلر ادمین است؛
    // اینجا فقط نوع‌های فعال، تفکیک‌شده به پایه و تکمیلی، در دسترس همهٔ کاربران واردشده.
    [ApiController]
    [Route("api/insurances")]
    public class InsurancesController : ControllerBase
    {
        private readonly ReSiRaiDbContext _context;
        public InsurancesController(ReSiRaiDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetActive()
        {
            var items = await _context.InsuranceTypes.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.InsuranceTypeName)
                .Select(x => new { x.InsuranceTypeID, x.InsuranceTypeName, x.IsSupplementary })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                baseInsurances = items.Where(x => !x.IsSupplementary).ToList(),
                supplementaryInsurances = items.Where(x => x.IsSupplementary).ToList()
            });
        }
    }
}
