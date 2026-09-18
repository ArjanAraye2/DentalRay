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
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var actions=await _db.StudyActions.AsNoTracking().Where(x=>x.StudyID==studyID).OrderBy(x=>x.StudyActionID).ToListAsync();
        var payments=await _db.StudyPayments.AsNoTracking().Where(x=>x.StudyID==studyID).OrderByDescending(x=>x.PaymentDate).ThenByDescending(x=>x.StudyPaymentID).ToListAsync();
        decimal gross=actions.Sum(x=>x.Amount),discount=actions.Sum(x=>x.DiscountAmount),received=payments.Sum(x=>x.Amount);
        return Ok(new { success=true,studyID,actions,payments,summary=new { grossAmount=gross,discountAmount=discount,netAmount=gross-discount,receivedAmount=received,balanceAmount=gross-discount-received } });
    }

    [HttpPost("actions")]
    public async Task<IActionResult> AddAction(int studyID, StudyActionRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var error=ValidateAction(request); if(error!=null)return BadRequest(new { success=false,message=error });
        var row=new StudyAction{StudyID=studyID,Description=request.Description.Trim(),Amount=request.Amount,DiscountAmount=request.DiscountAmount,CreatedDate=DateTime.Now};
        _db.StudyActions.Add(row);await _db.SaveChangesAsync();return Ok(new { success=true,action=row });
    }

    [HttpPut("actions/{id:long}")]
    public async Task<IActionResult> UpdateAction(int studyID,long id,StudyActionRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var error=ValidateAction(request);if(error!=null)return BadRequest(new { success=false,message=error });
        var row=await _db.StudyActions.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyActionID==id);if(row==null)return NotFound(new{success=false,message="Action not found."});
        row.Description=request.Description.Trim();row.Amount=request.Amount;row.DiscountAmount=request.DiscountAmount;row.ModifiedDate=DateTime.Now;await _db.SaveChangesAsync();return Ok(new{success=true,action=row});
    }

    [HttpDelete("actions/{id:long}")]
    public async Task<IActionResult> DeleteAction(int studyID,long id)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var row=await _db.StudyActions.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyActionID==id);if(row==null)return NotFound(new{success=false,message="Action not found."});
        _db.StudyActions.Remove(row);await _db.SaveChangesAsync();return Ok(new{success=true});
    }

    [HttpPost("payments")]
    public async Task<IActionResult> AddPayment(int studyID,StudyPaymentRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var error=ValidatePayment(request);if(error!=null)return BadRequest(new{success=false,message=error});
        var row=new StudyPayment{StudyID=studyID,PaymentDate=request.PaymentDate==default?DateTime.Now:request.PaymentDate,PaymentMethod=request.PaymentMethod,Amount=request.Amount,Description=Clean(request.Description),CreatedDate=DateTime.Now};
        _db.StudyPayments.Add(row);await _db.SaveChangesAsync();return Ok(new{success=true,payment=row});
    }

    [HttpPut("payments/{id:long}")]
    public async Task<IActionResult> UpdatePayment(int studyID,long id,StudyPaymentRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var error=ValidatePayment(request);if(error!=null)return BadRequest(new{success=false,message=error});
        var row=await _db.StudyPayments.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyPaymentID==id);if(row==null)return NotFound(new{success=false,message="Payment not found."});
        row.PaymentDate=request.PaymentDate;row.PaymentMethod=request.PaymentMethod;row.Amount=request.Amount;row.Description=Clean(request.Description);row.ModifiedDate=DateTime.Now;await _db.SaveChangesAsync();return Ok(new{success=true,payment=row});
    }

    [HttpDelete("payments/{id:long}")]
    public async Task<IActionResult> DeletePayment(int studyID,long id)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="Study not found." });
        var row=await _db.StudyPayments.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyPaymentID==id);if(row==null)return NotFound(new{success=false,message="Payment not found."});
        _db.StudyPayments.Remove(row);await _db.SaveChangesAsync();return Ok(new{success=true});
    }

    private Task<bool> CanAccess(int studyID)=>studyID>0?_access.CanAccessStudyAsync(studyID,User):Task.FromResult(false);
    private static string? ValidateAction(StudyActionRequest x){if(string.IsNullOrWhiteSpace(x.Description))return "Action description is required.";if(x.Description.Trim().Length>500)return "Action description is too long.";if(x.Amount<0)return "Action amount cannot be negative.";if(x.DiscountAmount<0||x.DiscountAmount>x.Amount)return "Discount must be between zero and the action amount.";return null;}
    private static string? ValidatePayment(StudyPaymentRequest x){if(x.Amount<=0)return "Payment amount must be greater than zero.";if(x.PaymentDate==default)return "Payment date is required.";if(x.PaymentMethod is null or <1 or >3)return "نوع دریافت را انتخاب کنید.";if(x.Description is not null&&x.Description.Trim().Length>500)return "Payment description is too long.";return null;}
    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed class StudyActionRequest { public string Description { get; set; }=string.Empty; public decimal Amount { get; set; } public decimal DiscountAmount { get; set; } }
public sealed class StudyPaymentRequest { public DateTime PaymentDate { get; set; } public byte? PaymentMethod { get; set; } public decimal Amount { get; set; } public string? Description { get; set; } }
