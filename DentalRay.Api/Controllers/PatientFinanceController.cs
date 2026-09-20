using DentalRay.Api.Data;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Patient-level finance summary.
    //
    // The Study finance panel answers "what does this Study cost", but the
    // repeated question at the desk is "how much does this patient still owe".
    // That needs one number across every Study the caller is allowed to see, so
    // the same StudyAccessService rules are applied here rather than summing
    // blindly over all of a patient's Studies.
    [ApiController]
    [Route("api/patients/{patientID:int}/finance")]
    public class PatientFinanceController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly StudyAccessService _access;

        public PatientFinanceController(DentalRayDbContext db, StudyAccessService access)
        {
            _db = db;
            _access = access;
        }

        [HttpGet]
        public async Task<IActionResult> Get(int patientID)
        {
            if (patientID <= 0)
                return BadRequest(new { success = false, message = "شناسه بیمار معتبر نیست." });

            bool patientExists = await _db.Patients.AsNoTracking().AnyAsync(p => p.PatientID == patientID);
            if (!patientExists)
                return NotFound(new { success = false, message = "بیمار پیدا نشد." });

            // Only the Studies this user is allowed to see contribute to the totals.
            var accessibleStudyIDs = await _access.ApplyAccess(
                _db.RadiologyStudies.AsNoTracking().Where(s => s.PatientID == patientID), User)
                .Select(s => s.StudyID).ToListAsync();

            decimal gross = 0m, discount = 0m, received = 0m;
            if (accessibleStudyIDs.Count > 0)
            {
                gross = await _db.StudyActions.AsNoTracking()
                    .Where(a => accessibleStudyIDs.Contains(a.StudyID))
                    .SumAsync(a => (decimal?)a.Amount) ?? 0m;
                discount = await _db.StudyActions.AsNoTracking()
                    .Where(a => accessibleStudyIDs.Contains(a.StudyID))
                    .SumAsync(a => (decimal?)a.DiscountAmount) ?? 0m;
                received = await _db.StudyPayments.AsNoTracking()
                    .Where(p => accessibleStudyIDs.Contains(p.StudyID))
                    .SumAsync(p => p.IsRefund ? -(decimal?)p.Amount : (decimal?)p.Amount) ?? 0m;
            }

            decimal net = gross - discount;

            return Ok(new
            {
                success = true,
                patientID,
                studyCount = accessibleStudyIDs.Count,
                grossAmount = gross,
                discountAmount = discount,
                netAmount = net,
                receivedAmount = received,
                balanceAmount = net - received
            });
        }

        // Today's takings for the dashboard: every payment recorded today, split
        // by method, restricted to the Studies the caller can see.
        [HttpGet("/api/finance/today")]
        public async Task<IActionResult> Today()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            var accessibleStudyIDs = await _access.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User)
                .Select(s => s.StudyID).ToListAsync();

            if (accessibleStudyIDs.Count == 0)
                return Ok(new { success = true, date = today, count = 0, total = 0m, cash = 0m, pos = 0m, card = 0m, unknown = 0m });

            var rows = await _db.StudyPayments.AsNoTracking()
                .Where(p => accessibleStudyIDs.Contains(p.StudyID)
                         && p.PaymentDate >= today && p.PaymentDate < tomorrow)
                .Select(p => new { p.Amount, p.PaymentMethod, p.IsRefund })
                .ToListAsync();

            // A refund issued today should not read as takings, so it subtracts
            // from the method it reverses.
            decimal Signed(decimal amount, bool isRefund) => isRefund ? -amount : amount;
            decimal cash = rows.Where(r => r.PaymentMethod == 3).Sum(r => Signed(r.Amount, r.IsRefund));
            decimal pos = rows.Where(r => r.PaymentMethod == 1).Sum(r => Signed(r.Amount, r.IsRefund));
            decimal card = rows.Where(r => r.PaymentMethod == 2).Sum(r => Signed(r.Amount, r.IsRefund));
            decimal unknown = rows.Where(r => r.PaymentMethod is null).Sum(r => Signed(r.Amount, r.IsRefund));

            return Ok(new
            {
                success = true,
                date = today,
                count = rows.Count,
                total = rows.Sum(r => Signed(r.Amount, r.IsRefund)),
                refundCount = rows.Count(r => r.IsRefund),
                cash,
                pos,
                card,
                unknown
            });
        }
    }
}
