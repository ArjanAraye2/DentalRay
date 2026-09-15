using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/organizations")]
    public class OrganizationsController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        public OrganizationsController(DentalRayDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetOrganizations() =>
            Ok(await _context.Organizations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());

        [HttpPost]
        public async Task<IActionResult> CreateOrganization(Organization item)
        {
            item.OrganizationID = 0;
            item.OrganizationGuid = Guid.NewGuid();
            item.Name = (item.Name ?? "").Trim();
            if (item.Name.Length == 0) return BadRequest(new { success = false, message = "Organization name is required." });
            item.CreatedDate = DateTime.Now; item.ModifiedDate = null; item.IsActive = true;
            _context.Organizations.Add(item); await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpGet("dentists/search")]
        public async Task<IActionResult> SearchDentists([FromQuery] string? q = null, [FromQuery] int? organizationID = null)
        {
            var query = from p in _context.Persons.AsNoTracking()
                        where p.IsActive
                        select p;

            if (organizationID.HasValue)
            {
                query = from p in query
                        join m in _context.OrganizationMembers.AsNoTracking() on p.PersonID equals m.PersonID
                        where m.OrganizationID == organizationID.Value && m.IsActive
                        select p;
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                string term = q.Trim();
                query = query.Where(p => p.FirstName.Contains(term) || p.LastName.Contains(term) ||
                                         (p.MedicalCouncilCode != null && p.MedicalCouncilCode.Contains(term)));
            }

            return Ok(await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(20).ToListAsync());
        }

        [HttpGet("{organizationID:int}/dentists")]
        public async Task<IActionResult> GetDentists(int organizationID)
        {
            var query = from m in _context.OrganizationMembers.AsNoTracking()
                        join p in _context.Persons.AsNoTracking() on m.PersonID equals p.PersonID
                        where m.OrganizationID == organizationID && m.IsActive && p.IsActive
                        orderby p.LastName, p.FirstName
                        select p;
            return Ok(await query.ToListAsync());
        }

        [HttpPost("{organizationID:int}/dentists")]
        public async Task<IActionResult> QuickCreateDentist(int organizationID, Person person)
        {
            if (!await _context.Organizations.AnyAsync(x => x.OrganizationID == organizationID && x.IsActive))
                return NotFound(new { success = false, message = "Organization not found." });
            person.PersonID = 0; person.PersonGuid = Guid.NewGuid();
            person.FirstName = (person.FirstName ?? "").Trim(); person.LastName = (person.LastName ?? "").Trim();
            person.PositionName = string.IsNullOrWhiteSpace(person.PositionName) ? "دندانپزشک" : person.PositionName.Trim();
            if (person.FirstName.Length == 0 || person.LastName.Length == 0)
                return BadRequest(new { success = false, message = "Dentist first name and last name are required." });
            person.CreatedDate = DateTime.Now; person.IsActive = true;

            // شماره نظام پزشکی، در صورت ثبت، باید برای هر شخص یکتا باشد.
            if (!string.IsNullOrWhiteSpace(person.MedicalCouncilCode))
            {
                person.MedicalCouncilCode = person.MedicalCouncilCode.Trim();
                bool duplicateCouncilCode = await _context.Persons.AnyAsync(p =>
                    p.MedicalCouncilCode == person.MedicalCouncilCode);
                if (duplicateCouncilCode)
                    return Conflict(new { success = false, message = "A person with this MedicalCouncilCode already exists." });
            }

            // هر دو رکورد باید با هم ثبت شوند؛ اگر ایجاد عضویت شکست خورد،
            // شخص نیمه‌کاره در دیتابیس باقی نماند.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                _context.OrganizationMembers.Add(new OrganizationMember
                {
                    OrganizationID = organizationID,
                    PersonID = person.PersonID,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(person);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}