using System.Security.Claims;
using ReSiRai.Api.Data;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// گزارش‌های مالی، مراجعات و تصویر برای یک بازهٔ تاریخی.
//
// دسترسی: مدیرِ سیستم، یا هر کاربری که پرچمِ «دسترسی به گزارش‌ها» را دارد
// (مثلاً حسابدار). برای دیگران این آدرس اصلاً وجود ندارد.
[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReSiRaiDbContext _db;

    public ReportsController(ReSiRaiDbContext db)
    {
        _db = db;
    }

    private bool CanView =>
        StudyAccessService.IsSuperAdmin(User) ||
        string.Equals(User.FindFirstValue("ViewReports"), "true", StringComparison.OrdinalIgnoreCase);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] string? from, [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!CanView) return NotFound(new { success = false, message = "پیدا نشد." });

        // بازهٔ پیش‌فرض: همین ماه. «تا» شاملِ خودِ روز است.
        DateTime fromDate = DateTime.TryParse(from, out var f) ? f.Date : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime toDate = DateTime.TryParse(to, out var t) ? t.Date : DateTime.Today;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);
        DateTime toExclusive = toDate.AddDays(1);

        // ---- مالی: دریافتی‌ها و بازپرداخت‌ها به تفکیکِ روش و روز --------------
        var paymentRows = await _db.StudyPayments.AsNoTracking()
            .Where(p => p.PaymentDate >= fromDate && p.PaymentDate < toExclusive)
            .Select(p => new { p.PaymentDate, p.PaymentMethod, p.Amount, p.IsRefund })
            .ToListAsync(cancellationToken);

        decimal received = paymentRows.Where(x => !x.IsRefund).Sum(x => x.Amount);
        decimal refunded = paymentRows.Where(x => x.IsRefund).Sum(x => x.Amount);
        int paymentCount = paymentRows.Count(x => !x.IsRefund);
        int refundCount = paymentRows.Count(x => x.IsRefund);

        var byMethod = paymentRows.Where(x => !x.IsRefund)
            .GroupBy(x => x.PaymentMethod)
            .Select(g => new
            {
                method = g.Key,
                label = MethodLabel(g.Key),
                count = g.Count(),
                amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.amount)
            .ToList();

        var daily = paymentRows
            .GroupBy(x => x.PaymentDate.Date)
            .Select(g => new
            {
                date = g.Key,
                count = g.Count(x => !x.IsRefund),
                received = g.Where(x => !x.IsRefund).Sum(x => x.Amount),
                refunded = g.Where(x => x.IsRefund).Sum(x => x.Amount)
            })
            .OrderBy(x => x.date)
            .ToList();

        // کارهای انجام‌شده و تخفیف‌ها در همان بازه (تاریخِ ثبتِ اقدام).
        var actionRows = await _db.StudyActions.AsNoTracking()
            .Where(a => a.CreatedDate >= fromDate && a.CreatedDate < toExclusive)
            .Select(a => new { a.Amount, a.DiscountAmount })
            .ToListAsync(cancellationToken);
        decimal performed = actionRows.Sum(x => x.Amount);
        decimal discounted = actionRows.Sum(x => x.DiscountAmount);

        // ---- مراجعات ---------------------------------------------------------
        var studyRows = await _db.RadiologyStudies.AsNoTracking()
            .Where(s => s.StudyDate >= fromDate && s.StudyDate < toExclusive)
            .Select(s => new { s.PatientID, s.StudyTypeID, s.DentistStaffID })
            .ToListAsync(cancellationToken);

        var typeNames = await _db.StudyTypes.AsNoTracking()
            .ToDictionaryAsync(x => x.StudyTypeID, x => x.StudyTypeName, cancellationToken);
        var staffNames = await _db.Staff.AsNoTracking()
            .ToDictionaryAsync(x => x.StaffID, x => (x.FirstName + " " + x.LastName).Trim(), cancellationToken);

        var byStudyType = studyRows.GroupBy(x => x.StudyTypeID)
            .Select(g => new { name = typeNames.TryGetValue(g.Key, out var n) ? n : $"نوع {g.Key}", count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var byDentist = studyRows.Where(x => x.DentistStaffID.HasValue)
            .GroupBy(x => x.DentistStaffID!.Value)
            .Select(g => new { name = staffNames.TryGetValue(g.Key, out var n) && n.Length > 0 ? n : $"پرسنل {g.Key}", count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        int newPatients = await _db.Patients.AsNoTracking()
            .CountAsync(p => p.CreatedDate >= fromDate && p.CreatedDate < toExclusive, cancellationToken);

        // ---- تصاویر ----------------------------------------------------------
        var imageRows = await _db.RadiologyImages.AsNoTracking()
            .Where(i => i.CreatedDate >= fromDate && i.CreatedDate < toExclusive)
            .Select(i => new { i.ImageTypeID })
            .ToListAsync(cancellationToken);
        var imageTypeNames = await _db.ImageTypes.AsNoTracking()
            .ToDictionaryAsync(x => x.ImageTypeID, x => x.ImageTypeName, cancellationToken);

        var imagesByType = imageRows.GroupBy(x => x.ImageTypeID)
            .Select(g => new { name = g.Key.HasValue && imageTypeNames.TryGetValue(g.Key.Value, out var n) ? n : "تعیین‌نشده", count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // ---- بدهکاران (بدونِ محدودیتِ تاریخ: همان ماندهٔ کل) ------------------
        var actionTotals = await (
            from a in _db.StudyActions.AsNoTracking()
            join s in _db.RadiologyStudies.AsNoTracking() on a.StudyID equals s.StudyID
            group new { a.Amount, a.DiscountAmount } by s.PatientID into g
            select new { PatientID = g.Key, Gross = g.Sum(x => x.Amount), Discount = g.Sum(x => x.DiscountAmount) })
            .ToListAsync(cancellationToken);

        var paymentTotals = await (
            from p in _db.StudyPayments.AsNoTracking()
            join s in _db.RadiologyStudies.AsNoTracking() on p.StudyID equals s.StudyID
            group new { p.Amount, p.IsRefund } by s.PatientID into g
            select new { PatientID = g.Key, Paid = g.Sum(x => x.IsRefund ? -x.Amount : x.Amount) })
            .ToListAsync(cancellationToken);

        var paidByPatient = paymentTotals.ToDictionary(x => x.PatientID, x => x.Paid);
        var balances = actionTotals
            .Select(x => new
            {
                x.PatientID,
                Balance = x.Gross - x.Discount - (paidByPatient.TryGetValue(x.PatientID, out var paid) ? paid : 0m)
            })
            .Where(x => x.Balance > 0)
            .OrderByDescending(x => x.Balance)
            .Take(100)
            .ToList();

        var debtorIDs = balances.Select(x => x.PatientID).ToList();
        var debtorPatients = new Dictionary<int, (string Name, string? Mobile)>();
        if (debtorIDs.Count > 0)
        {
            debtorPatients = await _db.Patients.AsNoTracking()
                .Where(p => debtorIDs.Contains(p.PatientID))
                .ToDictionaryAsync(p => p.PatientID,
                    p => (Name: (p.FirstName + " " + p.LastName).Trim(), Mobile: p.Mobile),
                    cancellationToken);
        }

        var debtors = balances
            .Where(x => debtorPatients.ContainsKey(x.PatientID))
            .Select(x => new
            {
                patientID = x.PatientID,
                name = debtorPatients[x.PatientID].Name,
                mobile = debtorPatients[x.PatientID].Mobile,
                balance = x.Balance
            })
            .ToList();

        return Ok(new
        {
            success = true,
            from = fromDate,
            to = toDate,
            generatedAt = DateTime.Now,
            finance = new
            {
                received,
                refunded,
                discounted,
                performed,
                net = performed - discounted,
                paymentCount,
                refundCount,
                byMethod,
                daily
            },
            visits = new
            {
                count = studyRows.Count,
                patientCount = studyRows.Select(x => x.PatientID).Distinct().Count(),
                newPatients,
                byStudyType,
                byDentist
            },
            images = new
            {
                total = imageRows.Count,
                byType = imagesByType
            },
            debtors
        });
    }

    private static string MethodLabel(byte? method) => method switch
    {
        1 => "کارتخوان",
        2 => "کارت به کارت",
        3 => "نقد",
        _ => "نامشخص"
    };
}
