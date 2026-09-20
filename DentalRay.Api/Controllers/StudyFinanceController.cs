using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using DentalRay.Api.Services.Pos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

[ApiController]
[Route("api/studies/{studyID:int}/finance")]
public class StudyFinanceController : ControllerBase
{
    private readonly DentalRayDbContext _db;
    private readonly StudyAccessService _access;
    private readonly PosProtocolRegistry _posProtocols;
    private readonly ILogger<StudyFinanceController> _logger;

    public StudyFinanceController(DentalRayDbContext db, StudyAccessService access,
        PosProtocolRegistry posProtocols, ILogger<StudyFinanceController> logger)
    {
        _db = db; _access = access; _posProtocols = posProtocols; _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(int studyID)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var actions=await _db.StudyActions.AsNoTracking().Where(x=>x.StudyID==studyID).OrderBy(x=>x.StudyActionID).ToListAsync();
        var payments=await _db.StudyPayments.AsNoTracking().Where(x=>x.StudyID==studyID).OrderByDescending(x=>x.PaymentDate).ThenByDescending(x=>x.StudyPaymentID).ToListAsync();
        decimal gross=actions.Sum(x=>x.Amount),discount=actions.Sum(x=>x.DiscountAmount);
        // A refund reverses money, so it subtracts from what was received.
        decimal received=payments.Sum(x=>x.IsRefund ? -x.Amount : x.Amount);
        return Ok(new { success=true,studyID,actions,payments,summary=new { grossAmount=gross,discountAmount=discount,netAmount=gross-discount,receivedAmount=received,balanceAmount=gross-discount-received } });
    }

    [HttpPost("actions")]
    public async Task<IActionResult> AddAction(int studyID, StudyActionRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var actionError=ValidateAction(request); if(actionError!=null)return BadRequest(new { success=false,message=actionError });
        var row=new StudyAction{StudyID=studyID,Description=request.Description.Trim(),Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero),DiscountAmount=Math.Round(request.DiscountAmount,2,MidpointRounding.AwayFromZero),CreatedDate=DateTime.Now};
        _db.StudyActions.Add(row);await _db.SaveChangesAsync();return Ok(new { success=true,action=row });
    }

    [HttpPut("actions/{id:long}")]
    public async Task<IActionResult> UpdateAction(int studyID,long id,StudyActionRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var actionError=ValidateAction(request);if(actionError!=null)return BadRequest(new{success=false,message=actionError});
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
        var paymentError=ValidatePayment(request);if(paymentError!=null)return BadRequest(new{success=false,message=paymentError});

        // Never accept more money than the Study actually costs. Without this a
        // payment larger than the charge silently produced a negative balance.
        var overError = await CheckOverpaymentAsync(studyID, request, null);
        if (overError != null) return BadRequest(new { success = false, message = overError });

        var row=new StudyPayment{
            StudyID=studyID,
            PaymentDate=request.PaymentDate==default?DateTime.Now:request.PaymentDate,
            PaymentMethod=request.PaymentMethod,
            Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero),
            IsRefund=request.IsRefund,
            Description=Clean(request.Description),
            CreatedDate=DateTime.Now};
        _db.StudyPayments.Add(row);await _db.SaveChangesAsync();return Ok(new{success=true,payment=row});
    }

    [HttpPut("payments/{id:long}")]
    public async Task<IActionResult> UpdatePayment(int studyID,long id,StudyPaymentRequest request)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var paymentError2=ValidatePayment(request);if(paymentError2!=null)return BadRequest(new{success=false,message=paymentError2});
        var row=await _db.StudyPayments.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyPaymentID==id);if(row==null)return NotFound(new{success=false,message="دریافت پیدا نشد."});

        // The row being edited must not count twice towards the total.
        var overError = await CheckOverpaymentAsync(studyID, request, row);
        if (overError != null) return BadRequest(new { success = false, message = overError });

        row.PaymentDate=request.PaymentDate;
        row.PaymentMethod=request.PaymentMethod;
        row.Amount=Math.Round(request.Amount,2,MidpointRounding.AwayFromZero);
        row.IsRefund=request.IsRefund;
        row.Description=Clean(request.Description);
        row.ModifiedDate=DateTime.Now;await _db.SaveChangesAsync();return Ok(new{success=true,payment=row});
    }

    [HttpDelete("payments/{id:long}")]
    public async Task<IActionResult> DeletePayment(int studyID,long id)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success=false, message="مطالعه پیدا نشد." });
        var row=await _db.StudyPayments.FirstOrDefaultAsync(x=>x.StudyID==studyID&&x.StudyPaymentID==id);if(row==null)return NotFound(new{success=false,message="دریافت پیدا نشد."});
        _db.StudyPayments.Remove(row);await _db.SaveChangesAsync();return Ok(new{success=true});
    }

    // Sends a payment's amount to the card reader and records the outcome.
    //
    // The payment is saved first and the dispatch second on purpose: if the
    // terminal is unreachable the money is still on record and can be retried,
    // which is safer than the reverse order where a device could take money that
    // was never stored.
    [HttpPost("payments/{id:long}/send-to-pos")]
    public async Task<IActionResult> SendPaymentToPos(int studyID, long id, [FromQuery] int? posSettingID, CancellationToken cancellationToken)
    {
        if (!await CanAccess(studyID)) return NotFound(new { success = false, message = "مطالعه پیدا نشد." });

        var payment = await _db.StudyPayments.FirstOrDefaultAsync(x => x.StudyID == studyID && x.StudyPaymentID == id);
        if (payment == null) return NotFound(new { success = false, message = "دریافت پیدا نشد." });
        if (payment.IsRefund) return BadRequest(new { success = false, message = "مبلغ بازپرداخت به پوز فرستاده نمی‌شود." });

        var setting = posSettingID.HasValue
            ? await _db.PosSettings.FirstOrDefaultAsync(x => x.PosSettingID == posSettingID.Value)
            : await _db.PosSettings.Where(x => x.IsActive)
                .OrderByDescending(x => x.IsDefault).FirstOrDefaultAsync();

        if (setting == null)
            return BadRequest(new { success = false, message = "پوزی تنظیم نشده است. ابتدا در تنظیمات، دستگاه پوز را ثبت کنید." });
        if (!setting.IsActive)
            return BadRequest(new { success = false, message = "پوز انتخاب‌شده غیرفعال است." });
        if (setting.ConnectionType != 1)
            return BadRequest(new { success = false, message = "این نوع اتصال پوز هنوز پشتیبانی نمی‌شود." });

        var protocol = _posProtocols.Resolve(setting.Protocol);
        PosResult result;
        try
        {
            result = await protocol.SendAmountAsync(new PosRequest
            {
                Host = setting.Host ?? string.Empty,
                Port = setting.Port ?? 0,
                Amount = payment.Amount,
                Invoice = payment.StudyPaymentID.ToString(),
                RequestPattern = setting.RequestPattern,
                SuccessPattern = setting.SuccessPattern,
                Encoding = setting.Encoding,
                TimeoutSeconds = setting.TimeoutSeconds
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POS dispatch failed for payment {PaymentID}", id);
            result = PosResult.Fail("ارتباط با پوز با خطا مواجه شد.");
        }

        payment.PosSettingID = setting.PosSettingID;
        payment.PosSentAt = DateTime.Now;
        payment.PosSuccess = result.Success;
        payment.PosMessage = Truncate(result.Success
            ? result.Message
            : $"{result.Message}{(string.IsNullOrWhiteSpace(result.RawResponse) ? "" : " | " + result.RawResponse)}", 500);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            ok = result.Success,
            message = result.Message,
            raw = result.RawResponse,
            posName = setting.Name,
            sentAt = payment.PosSentAt
        });
    }

    // Compares what the Study costs with what has already been received.
    private async Task<string?> CheckOverpaymentAsync(int studyID, StudyPaymentRequest request, StudyPayment? editing)
    {
        if (request.IsRefund) return null;   // a refund reduces received, it cannot overpay

        decimal gross = await _db.StudyActions.AsNoTracking()
            .Where(a => a.StudyID == studyID).SumAsync(a => (decimal?)a.Amount) ?? 0m;
        decimal discount = await _db.StudyActions.AsNoTracking()
            .Where(a => a.StudyID == studyID).SumAsync(a => (decimal?)a.DiscountAmount) ?? 0m;
        decimal net = gross - discount;

        // No charges recorded yet: an advance payment is legitimate, so allow it.
        if (net <= 0) return null;

        var others = await _db.StudyPayments.AsNoTracking()
            .Where(p => p.StudyID == studyID && (editing == null || p.StudyPaymentID != editing.StudyPaymentID))
            .Select(p => new { p.Amount, p.IsRefund }).ToListAsync();
        decimal alreadyReceived = others.Sum(p => p.IsRefund ? -p.Amount : p.Amount);

        decimal afterThis = alreadyReceived + Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (afterThis > net)
        {
            decimal remaining = Math.Max(0m, net - alreadyReceived);
            return $"مبلغ دریافت از هزینه مطالعه بیشتر است. مبلغ خالص {net:N0} و مانده قابل دریافت {remaining:N0} است.";
        }
        return null;
    }

    private Task<bool> CanAccess(int studyID)=>studyID>0?_access.CanAccessStudyAsync(studyID,User):Task.FromResult(false);

    // Amounts are stored as decimal(18,2). A value like 0.001 passes "> 0" in C#
    // but rounds to 0.00 in SQL, which violates CK_tblStudyPayments_Amount and
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
        var amountError = ValidateAmount(x.Amount, x.IsRefund ? "مبلغ بازپرداخت" : "مبلغ دریافت");
        if (amountError != null) return amountError;
        if (x.PaymentDate == default) return "تاریخ دریافت را وارد کنید.";
        if (x.PaymentDate.Year < 2000 || x.PaymentDate.Year > 2100) return "تاریخ دریافت معتبر نیست.";
        if (x.PaymentMethod is null or <1 or >3) return "نوع دریافت را انتخاب کنید.";
        if (x.Description is not null && x.Description.Trim().Length > 500) return "شرح دریافت نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.";
        // A refund must say why; the database enforces this too.
        if (x.IsRefund && (x.Description is null || x.Description.Trim().Length < 3))
            return "دلیل بازپرداخت را وارد کنید.";
        return null;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed class StudyActionRequest { public string Description { get; set; }=string.Empty; public decimal Amount { get; set; } public decimal DiscountAmount { get; set; } }
public sealed class StudyPaymentRequest { public DateTime PaymentDate { get; set; } public byte? PaymentMethod { get; set; } public decimal Amount { get; set; } public string? Description { get; set; } public bool IsRefund { get; set; } }
