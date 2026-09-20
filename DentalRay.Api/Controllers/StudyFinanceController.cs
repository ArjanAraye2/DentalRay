using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

[ApiController]
[Route("api/studies/{studyID:int}/finance")]
public class StudyFinanceController : ControllerBase
{
    private readonly DentalRayDbContext _db;
    private readonly StudyAccessService _access;
    public StudyFinanceController(DentalRayDbContext db, StudyAccessService access) { _db=db; _access=access; }

    [HttpGet]
    public async Task<IActionResult> Get(int studyID)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var actions=await _db.StudyActions.AsNoTracking().Where(x=>x.StudyID==studyID).OrderBy(x=>x.StudyActionID).ToListAsync();
        var payments=await _db.StudyPayments.AsNoTracking().Where(x=>x.StudyID==studyID).OrderByDescending(x=>x.PaymentDate).ThenByDescending(x=>x.StudyPaymentID).ToListAsync();
        decimal gross=actions.Sum(x=>x.Amount),discount=actions.Sum(x=>x.DiscountAmount),received=payments.Sum(x=>x.Amount);
        return Ok(new { success=true,studyID,actions,payments,summary=new { grossAmount=gross,discountAmount=discount,netAmount=gross-discount,receivedAmount=received,balanceAmount=gross-discount-received } });
    }

    [HttpPost("actions")]
    public async Task<IActionResult> AddAction(int studyID, StudyActionRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var error=ValidateAction(request); if(error!=null)return BadRequest(new { success=false,message=error });
        var row=new StudyAction{StudyID=studyID,Description=request.Description.Trim(),Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero),DiscountAmount=Math.Round(request.DiscountAmount,2,MidpointRounding.AwayFromZero),CreatedDate=DateTime.Now};
        _db.StudyActions.Add(row);await _db.SaveChangesAsync();return Ok(new { success=true,action=row });
    }

    [HttpPut("actions/{id:long}")]
    public async Task<IActionResult> UpdateAction(int studyID,long id,StudyActionRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var error=ValidateAction(request);if(error!=null)return BadRequest(new { success=false,message=error });
        var row=await _db.StudyActions.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyActionID==id);if(row==null)return NotFound(new{success=false,message="اقدام پیدا نشد."});
        row.Description=request.Description.Trim();row.Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero);row.DiscountAmount=Math.Round(request.DiscountAmount,2,MidpointRounding.AwayFromZero);row.ModifiedDate=DateTime.Now;await _db.SaveChangesAsync();return Ok(new{success=true,action=row});
    }

    [HttpDelete("actions/{id:long}")]
    public async Task<IActionResult> DeleteAction(int studyID,long id)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var row=await _db.StudyActions.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyActionID==id);if(row==null)return NotFound(new{success=false,message="اقدام پیدا نشد."});
        _db.StudyActions.Remove(row);await _db.SaveChangesAsync();return Ok(new{success=true});
    }

    [HttpPost("payments")]
    public async Task<IActionResult> AddPayment(int studyID,StudyPaymentRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var error=ValidatePayment(request);if(error!=null)return BadRequest(new{success=false,message=error});
        var row=new StudyPayment{StudyID=studyID,PaymentDate=request.PaymentDate==default?DateTime.Now:request.PaymentDate,PaymentMethod=request.PaymentMethod,Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero),Description=Clean(request.Description),CreatedDate=DateTime.Now};
        _db.StudyPayments.Add(row);await _db.SaveChangesAsync();return Ok(new{success=true,payment=row});
    }

    [HttpPut("payments/{id:long}")]
    public async Task<IActionResult> UpdatePayment(int studyID,long id,StudyPaymentRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var error=ValidatePayment(request);if(error!=null)return BadRequest(new{success=false,message=error});
        var row=await _db.StudyPayments.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyPaymentID==id);if(row==null)return NotFound(new{success=false,message="دریافت پیدا نشد."});
        row.PaymentDate=request.PaymentDate;row.PaymentMethod=request.PaymentMethod;row.Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero);row.Description=Clean(request.Description);row.ModifiedDate=DateTime.Now;await _db.SaveChangesAsync();return Ok(new{success=true,payment=row});
    }

    [HttpDelete("payments/{id:long}")]
    public async Task<IActionResult> DeletePayment(int studyID,long id)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var row=await _db.StudyPayments.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyPaymentID==id);if(row==null)return NotFound(new{success=false,message="دریافت پیدا نشد."});
        _db.StudyPayments.Remove(row);await _db.SaveChangesAsync();return Ok(new{success=true});
    }

    private Task<bool> CanAccess(int studyID)=>studyID>0?_access.CanAccessStudyAsync(studyID,User):Task.FromResult(false);

    // Amounts are stored as decimal(18,2). A value like 0.001 passes "> 0" in C#
    // but rounds to 0.00 in SQL, which then violates CK_tblStudyPayments_Amount and
    // surfaced as a raw DbUpdateException. Rounding here keeps validation and the
    // database in agreement.
    private const decimal MaxAmount = 9_999_999_999m;   // ~100 billion Toman, far above any real fee

    private static string? ValidateAmount(decimal amount, string label)
    {
        decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (rounded <= 0) return $"{label} باید بیشتر از صفر باشد.";
        if (rounded > MaxAmount) return $"{label} بیش از حد بزرگ است.";
        return null;
    }

    private static string? ValidateAction(StudyActionRequest x)
    {
        if (string.IsNullOrWhiteSpace(x.Description)) return "شرح اقدام را وارد کنید.";
        if (x.Description.Trim().Length > 500) return "شرح اقدام نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.";
        var amountError = ValidateAmount(x.Amount, "مبلغ هزینه");
        if (amountError != null && x.Amount != 0) return amountError;   // 0 stays valid for a discounted/free action
        if (x.Amount < 0) return "مبلغ هزینه نمی‌تواند منفی باشد.";
        if (x.DiscountAmount < 0) return "مبلغ تخفیف نمی‌تواند منفی باشد.";
        if (Math.Round(x.DiscountAmount, 2) > Math.Round(x.Amount, 2)) return "مبلغ تخفیف نمی‌تواند بیشتر از هزینه باشد.";
        return null;
    }

    private static string? ValidatePayment(StudyPaymentRequest x)
    {
        var amountError = ValidateAmount(x.Amount, "مبلغ دریافت");
        if (amountError != null) return amountError;
        if (x.PaymentDate == default) return "تاریخ دریافت را وارد کنید.";
        if (x.PaymentDate.Year < 2000 || x.PaymentDate.Year > 2100) return "تاریخ دریافت معتبر نیست.";
        if (x.PaymentMethod is null or <1 or >3) return "نوع دریافت را انتخاب کنید.";
        if (x.Description is not null && x.Description.Trim().Length > 500) return "شرح دریافت نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.";
        return null;
    }

    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed class StudyActionRequest { public string Description { get; set; }=string.Empty; public decimal Amount { get; set; } public decimal DiscountAmount { get; set; } }
public sealed class StudyPaymentRequest { public DateTime PaymentDate { get; set; } public byte? PaymentMethod { get; set; } public decimal Amount { get; set; } public string? Description { get; set; } }
