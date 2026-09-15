using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/studyfinancials")]
    [Authorize]
    public class StudyFinancialsController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        public StudyFinancialsController(DentalRayDbContext context) => _context = context;

        [HttpGet("{studyID:int}")]
        public async Task<IActionResult> Get(int studyID)
        {
            var study = await _context.RadiologyStudies.AsNoTracking().FirstOrDefaultAsync(x => x.StudyID == studyID);
            if (study == null) return NotFound(new { success = false, message = "Study not found." });
            var actions = await _context.StudyActions.AsNoTracking().Where(x => x.StudyID == studyID).OrderBy(x => x.StudyActionID).ToListAsync();
            var payments = await _context.StudyPayments.AsNoTracking().Where(x => x.StudyID == studyID).OrderBy(x => x.PaymentDate).ThenBy(x => x.StudyPaymentID).ToListAsync();
            decimal gross = actions.Sum(x => x.Amount);
            decimal discount = CalculateDiscount(gross, study.DiscountType, study.DiscountValue);
            decimal net = Math.Max(0, gross - discount);
            decimal paid = payments.Sum(x => x.Amount);
            return Ok(new { success=true, actions, payments, grossAmount=gross, study.DiscountType, study.DiscountValue, discountAmount=discount, netAmount=net, paidAmount=paid, balanceAmount=net-paid });
        }

        [HttpPost("{studyID:int}/actions")]
        public async Task<IActionResult> AddAction(int studyID, StudyAction item)
        {
            if (!await _context.RadiologyStudies.AnyAsync(x => x.StudyID == studyID)) return NotFound(new { success=false, message="Study not found." });
            item.StudyActionID=0; item.StudyID=studyID; item.Title=(item.Title??"").Trim();
            if(item.Title.Length==0) return BadRequest(new {success=false,message="Action title is required."});
            if(item.Title.Length>200) return BadRequest(new {success=false,message="Action title cannot exceed 200 characters."});
            if(item.Amount<0) return BadRequest(new {success=false,message="Action amount cannot be negative."});
            item.Description=string.IsNullOrWhiteSpace(item.Description)?null:item.Description.Trim();
            if(item.Description?.Length>1000) return BadRequest(new {success=false,message="Action description cannot exceed 1000 characters."});
            item.CreatedDate=DateTime.Now; item.ModifiedDate=null;
            _context.StudyActions.Add(item); await _context.SaveChangesAsync();
            await RecalculateDiscount(studyID);
            return Ok(new { success = true, action = item });
        }

        // ویرایش یک اقدام مالی موجود.
        // StudyID از نشانی درخواست دریافت می‌شود و به داده ارسالی کاربر اعتماد نمی‌کنیم.
        [HttpPut("{studyID:int}/actions/{studyActionID:int}")]
        public async Task<IActionResult> UpdateAction(
            int studyID,
            int studyActionID,
            StudyAction request)
        {
            var item = await _context.StudyActions
                .FirstOrDefaultAsync(x =>
                    x.StudyActionID == studyActionID &&
                    x.StudyID == studyID);

            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Study action not found."
                });
            }

            var title = (request.Title ?? string.Empty).Trim();

            if (title.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Action title is required."
                });
            }

            if (title.Length > 200)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Action title cannot exceed 200 characters."
                });
            }

            if (request.Amount < 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Action amount cannot be negative."
                });
            }

            var description = request.Description?.Trim();

            if (description?.Length > 1000)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Action description cannot exceed 1000 characters."
                });
            }

            item.Title = title;
            item.Amount = request.Amount;
            item.Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description;
            item.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // با تغییر مبلغ اقدام، تخفیف درصدی نیز باید دوباره محاسبه شود.
            await RecalculateDiscount(studyID);

            return Ok(new { success = true, action = item });
        }

        // حذف اقدام پس از تأیید کاربر در Frontend انجام می‌شود.
        [HttpDelete("{studyID:int}/actions/{studyActionID:int}")]
        public async Task<IActionResult> DeleteAction(
            int studyID,
            int studyActionID)
        {
            var item = await _context.StudyActions
                .FirstOrDefaultAsync(x =>
                    x.StudyActionID == studyActionID &&
                    x.StudyID == studyID);

            if (item == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Study action not found."
                });
            }

            _context.StudyActions.Remove(item);
            await _context.SaveChangesAsync();

            // حذف اقدام جمع ناخالص و در نتیجه مبلغ تخفیف را تغییر می‌دهد.
            await RecalculateDiscount(studyID);

            return Ok(new
            {
                success = true,
                message = "Study action deleted successfully."
            });
        }

        [HttpPost("{studyID:int}/payments")]
        public async Task<IActionResult> AddPayment(int studyID, StudyPayment item)
        {
            if (!await _context.RadiologyStudies.AnyAsync(x => x.StudyID == studyID)) return NotFound(new { success=false, message="Study not found." });
            if(item.Amount<=0) return BadRequest(new {success=false,message="Payment amount must be greater than zero."});
            if(item.PaymentMethod<1 || item.PaymentMethod>3) return BadRequest(new {success=false,message="Invalid payment method."});

            item.ReferenceNumber=string.IsNullOrWhiteSpace(item.ReferenceNumber)?null:item.ReferenceNumber.Trim();
            item.Description=string.IsNullOrWhiteSpace(item.Description)?null:item.Description.Trim();

            if(item.ReferenceNumber?.Length>100)
                return BadRequest(new {success=false,message="Payment reference number cannot exceed 100 characters."});

            if(item.Description?.Length>1000)
                return BadRequest(new {success=false,message="Payment description cannot exceed 1000 characters."});

            item.StudyPaymentID=0; item.StudyID=studyID;
            if(item.PaymentDate==default) item.PaymentDate=DateTime.Now;
            item.CreatedDate=DateTime.Now;
            _context.StudyPayments.Add(item); await _context.SaveChangesAsync();
            return Ok(new { success = true, payment = item });
        }

        [HttpPut("{studyID:int}/discount")]
        public async Task<IActionResult> SetDiscount(int studyID, DiscountRequest request)
        {
            var study=await _context.RadiologyStudies.FirstOrDefaultAsync(x=>x.StudyID==studyID);
            if(study==null) return NotFound(new {success=false,message="Study not found."});
            if(request.DiscountType>2 || request.DiscountValue<0 || (request.DiscountType==2 && request.DiscountValue>100))
                return BadRequest(new {success=false,message="Invalid discount."});
            study.DiscountType=request.DiscountType; study.DiscountValue=request.DiscountType==0?0:request.DiscountValue;
            decimal gross=await _context.StudyActions.Where(x=>x.StudyID==studyID).SumAsync(x=>(decimal?)x.Amount)??0;
            study.DiscountAmount=CalculateDiscount(gross,study.DiscountType,study.DiscountValue);
            study.ModifiedDate=DateTime.Now; await _context.SaveChangesAsync();
            return Ok(new {success=true,study.DiscountType,study.DiscountValue,study.DiscountAmount});
        }

        private async Task RecalculateDiscount(int studyID)
        {
            var study=await _context.RadiologyStudies.FirstAsync(x=>x.StudyID==studyID);
            decimal gross=await _context.StudyActions.Where(x=>x.StudyID==studyID).SumAsync(x=>(decimal?)x.Amount)??0;
            study.DiscountAmount=CalculateDiscount(gross,study.DiscountType,study.DiscountValue);
            await _context.SaveChangesAsync();
        }

        private static decimal CalculateDiscount(decimal gross, byte type, decimal value) =>
            type==1 ? Math.Min(gross,Math.Round(value,0)) :
            type==2 ? Math.Min(gross,Math.Round(gross*value/100m,0)) : 0m;

        public class DiscountRequest { public byte DiscountType {get;set;} public decimal DiscountValue {get;set;} }
    }
}
