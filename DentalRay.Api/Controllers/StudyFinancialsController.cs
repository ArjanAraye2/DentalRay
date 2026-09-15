using System.Data;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
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
        private readonly StudyFinancialAuditService _financialAudit;
        private readonly ResourceAccessService _access;

        public StudyFinancialsController(
            DentalRayDbContext context,
            StudyFinancialAuditService financialAudit,
            ResourceAccessService access)
        {
            _context = context;
            _financialAudit = financialAudit;
            _access = access;
        }

        [HttpGet("{studyID:int}")]
        public async Task<IActionResult> Get(int studyID)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanManageStudyAsync(studyID, currentUserID) &&
                !User.IsInRole("Admin"))
                return NotFound(new { success = false, message = "Study not found." });

            var study = await _context.RadiologyStudies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudyID == studyID);

            if (study == null)
                return NotFound(new { success = false, message = "Study not found." });

            var actions = await _context.StudyActions
                .AsNoTracking()
                .Where(x => x.StudyID == studyID)
                .OrderBy(x => x.StudyActionID)
                .ToListAsync();

            var payments = await _context.StudyPayments
                .AsNoTracking()
                .Where(x => x.StudyID == studyID)
                .OrderBy(x => x.PaymentDate)
                .ThenBy(x => x.StudyPaymentID)
                .ToListAsync();

            decimal gross = actions.Sum(x => x.Amount);
            decimal discount = CalculateDiscount(
                gross,
                study.DiscountType,
                study.DiscountValue);
            decimal net = Math.Max(0, gross - discount);
            decimal paid = payments.Sum(x => x.Amount);

            return Ok(new
            {
                success = true,
                actions,
                payments,
                grossAmount = gross,
                study.DiscountType,
                study.DiscountValue,
                discountAmount = discount,
                netAmount = net,
                paidAmount = paid,
                balanceAmount = net - paid
            });
        }

        [HttpPost("{studyID:int}/actions")]
        public async Task<IActionResult> AddAction(
            int studyID,
            StudyActionRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanManageStudyAsync(studyID, currentUserID))
                return NotFound(new { success = false, message = "Study not found." });

            var validationError = ValidateAction(request);
            if (validationError != null)
                return BadRequest(new { success = false, message = validationError });

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var study = await _context.RadiologyStudies
                .FirstOrDefaultAsync(x => x.StudyID == studyID);

            if (study == null)
                return NotFound(new { success = false, message = "Study not found." });

            decimal grossBefore = await GetGrossAmount(studyID);
            var discountBefore = CreateDiscountSnapshot(study, grossBefore);

            var item = new StudyAction
            {
                StudyID = studyID,
                Title = request.Title.Trim(),
                Amount = request.Amount,
                Description = NormalizeOptionalText(request.Description),
                CreatedDate = DateTime.Now
            };

            _context.StudyActions.Add(item);
            await _context.SaveChangesAsync();

            decimal grossAfter = await GetGrossAmount(studyID);
            RecalculateDiscount(study, grossAfter);

            _financialAudit.Record(
                User,
                studyID,
                "StudyAction",
                item.StudyActionID,
                "Created",
                before: null,
                after: CreateActionSnapshot(item));

            RecordDiscountChangeIfNeeded(studyID, study, grossBefore, grossAfter, discountBefore);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true, action = item });
        }

        [HttpPut("{studyID:int}/actions/{studyActionID:int}")]
        public async Task<IActionResult> UpdateAction(
            int studyID,
            int studyActionID,
            StudyActionRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanManageStudyAsync(studyID, currentUserID))
                return NotFound(new { success = false, message = "Study action not found." });

            var validationError = ValidateAction(request);
            if (validationError != null)
                return BadRequest(new { success = false, message = validationError });

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var item = await _context.StudyActions
                .FirstOrDefaultAsync(x =>
                    x.StudyActionID == studyActionID &&
                    x.StudyID == studyID);

            if (item == null)
                return NotFound(new { success = false, message = "Study action not found." });

            var study = await _context.RadiologyStudies
                .FirstAsync(x => x.StudyID == studyID);

            decimal grossBefore = await GetGrossAmount(studyID);
            var discountBefore = CreateDiscountSnapshot(study, grossBefore);
            var actionBefore = CreateActionSnapshot(item);

            item.Title = request.Title.Trim();
            item.Amount = request.Amount;
            item.Description = NormalizeOptionalText(request.Description);
            item.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            decimal grossAfter = await GetGrossAmount(studyID);
            RecalculateDiscount(study, grossAfter);

            _financialAudit.Record(
                User,
                studyID,
                "StudyAction",
                item.StudyActionID,
                "Updated",
                actionBefore,
                CreateActionSnapshot(item));

            RecordDiscountChangeIfNeeded(studyID, study, grossBefore, grossAfter, discountBefore);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true, action = item });
        }

        [HttpDelete("{studyID:int}/actions/{studyActionID:int}")]
        public async Task<IActionResult> DeleteAction(
            int studyID,
            int studyActionID)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanManageStudyAsync(studyID, currentUserID))
                return NotFound(new { success = false, message = "Study action not found." });

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var item = await _context.StudyActions
                .FirstOrDefaultAsync(x =>
                    x.StudyActionID == studyActionID &&
                    x.StudyID == studyID);

            if (item == null)
                return NotFound(new { success = false, message = "Study action not found." });

            var study = await _context.RadiologyStudies
                .FirstAsync(x => x.StudyID == studyID);

            decimal grossBefore = await GetGrossAmount(studyID);
            var discountBefore = CreateDiscountSnapshot(study, grossBefore);
            var actionBefore = CreateActionSnapshot(item);

            _context.StudyActions.Remove(item);
            await _context.SaveChangesAsync();

            decimal grossAfter = await GetGrossAmount(studyID);
            RecalculateDiscount(study, grossAfter);

            // حذف ردیف اصلی مجاز است، اما تصویر قبل از حذف در Audit باقی می‌ماند.
            _financialAudit.Record(
                User,
                studyID,
                "StudyAction",
                studyActionID,
                "Deleted",
                actionBefore,
                after: null);

            RecordDiscountChangeIfNeeded(studyID, study, grossBefore, grossAfter, discountBefore);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                message = "Study action deleted successfully."
            });
        }

        [HttpPost("{studyID:int}/payments")]
        public async Task<IActionResult> AddPayment(
            int studyID,
            StudyPaymentRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanManageStudyAsync(studyID, currentUserID))
                return NotFound(new { success = false, message = "Study not found." });

            var validationError = ValidatePayment(request);
            if (validationError != null)
                return BadRequest(new { success = false, message = validationError });

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var study = await _context.RadiologyStudies
                .FirstOrDefaultAsync(x => x.StudyID == studyID);

            if (study == null)
                return NotFound(new { success = false, message = "Study not found." });

            decimal netAmount = await GetNetAmount(study);
            decimal paidAmount = await _context.StudyPayments
                .Where(x => x.StudyID == studyID)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            var overpayment = CheckOverpayment(
                request.Amount,
                netAmount - paidAmount,
                request.ConfirmOverpayment);

            if (overpayment != null)
            {
                await transaction.RollbackAsync();
                return Conflict(overpayment);
            }

            var item = new StudyPayment
            {
                StudyID = studyID,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                PaymentDate = request.PaymentDate == default
                    ? DateTime.Now
                    : request.PaymentDate,
                ReferenceNumber = NormalizeOptionalText(request.ReferenceNumber),
                Description = NormalizeOptionalText(request.Description),
                CreatedDate = DateTime.Now
            };

            _context.StudyPayments.Add(item);
            await _context.SaveChangesAsync();

            decimal balanceBefore = netAmount - paidAmount;
            decimal balanceAfter = balanceBefore - item.Amount;

            _financialAudit.Record(
                User,
                studyID,
                "StudyPayment",
                item.StudyPaymentID,
                "Created",
                before: null,
                after: CreatePaymentSnapshot(item),
                notes: request.ConfirmOverpayment && balanceAfter < 0
                    ? $"Overpayment explicitly confirmed; excess={Math.Abs(balanceAfter):0}."
                    : null);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true, payment = item });
        }

        // اصلاح پرداخت از نظر مجوز فقط برای مدیر ممکن است.
        // تغییرات قبلی با مقدار قبل/بعد در جدول مستقل باقی می‌مانند.
        [HttpPut("{studyID:int}/payments/{studyPaymentID:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdatePayment(
            int studyID,
            int studyPaymentID,
            StudyPaymentRequest request)
        {
            var validationError = ValidatePayment(request);
            if (validationError != null)
                return BadRequest(new { success = false, message = validationError });

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var item = await _context.StudyPayments
                .FirstOrDefaultAsync(x =>
                    x.StudyPaymentID == studyPaymentID &&
                    x.StudyID == studyID);

            if (item == null)
                return NotFound(new { success = false, message = "Study payment not found." });

            var study = await _context.RadiologyStudies
                .FirstAsync(x => x.StudyID == studyID);

            decimal netAmount = await GetNetAmount(study);
            decimal paidWithoutThisPayment = await _context.StudyPayments
                .Where(x => x.StudyID == studyID && x.StudyPaymentID != studyPaymentID)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            var overpayment = CheckOverpayment(
                request.Amount,
                netAmount - paidWithoutThisPayment,
                request.ConfirmOverpayment);

            if (overpayment != null)
            {
                await transaction.RollbackAsync();
                return Conflict(overpayment);
            }

            var before = CreatePaymentSnapshot(item);

            item.Amount = request.Amount;
            item.PaymentMethod = request.PaymentMethod;
            item.PaymentDate = request.PaymentDate == default
                ? item.PaymentDate
                : request.PaymentDate;
            item.ReferenceNumber = NormalizeOptionalText(request.ReferenceNumber);
            item.Description = NormalizeOptionalText(request.Description);
            item.ModifiedDate = DateTime.Now;

            decimal balanceAfter = netAmount - paidWithoutThisPayment - item.Amount;

            _financialAudit.Record(
                User,
                studyID,
                "StudyPayment",
                item.StudyPaymentID,
                "Updated",
                before,
                CreatePaymentSnapshot(item),
                notes: request.ConfirmOverpayment && balanceAfter < 0
                    ? $"Overpayment explicitly confirmed; excess={Math.Abs(balanceAfter):0}."
                    : null);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true, payment = item });
        }

        [HttpPut("{studyID:int}/discount")]
        public async Task<IActionResult> SetDiscount(
            int studyID,
            StudyDiscountRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanManageStudyAsync(studyID, currentUserID))
                return NotFound(new { success = false, message = "Study not found." });

            if (request.DiscountType > 2 ||
                request.DiscountValue < 0 ||
                (request.DiscountType == 2 && request.DiscountValue > 100))
            {
                return BadRequest(new { success = false, message = "Invalid discount." });
            }

            if (request.DiscountType == 1 && !IsWholeRial(request.DiscountValue))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Discount amount must be a whole number of rials."
                });
            }

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var study = await _context.RadiologyStudies
                .FirstOrDefaultAsync(x => x.StudyID == studyID);

            if (study == null)
                return NotFound(new { success = false, message = "Study not found." });

            decimal gross = await GetGrossAmount(studyID);
            var before = CreateDiscountSnapshot(study, gross);

            study.DiscountType = request.DiscountType;
            study.DiscountValue = request.DiscountType == 0
                ? 0
                : request.DiscountValue;
            study.DiscountAmount = CalculateDiscount(
                gross,
                study.DiscountType,
                study.DiscountValue);
            study.ModifiedDate = DateTime.Now;

            var after = CreateDiscountSnapshot(study, gross);

            _financialAudit.Record(
                User,
                studyID,
                "StudyDiscount",
                studyID,
                "Updated",
                before,
                after);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                study.DiscountType,
                study.DiscountValue,
                study.DiscountAmount
            });
        }

        // فقط مدیر به تاریخچه کامل مقادیر قبل/بعد دسترسی دارد.
        [HttpGet("{studyID:int}/audit")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetFinancialAudit(int studyID)
        {
            if (!await _context.RadiologyStudies
                    .AsNoTracking()
                    .AnyAsync(x => x.StudyID == studyID))
            {
                return NotFound(new { success = false, message = "Study not found." });
            }

            var entries = await _context.StudyFinancialAuditLogs
                .AsNoTracking()
                .Where(x => x.StudyID == studyID)
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.StudyFinancialAuditLogID)
                .ToListAsync();

            return Ok(new { success = true, entries });
        }

        private static object? CheckOverpayment(
            decimal requestedAmount,
            decimal currentBalance,
            bool confirmed)
        {
            decimal excessAmount = requestedAmount - currentBalance;

            if (excessAmount <= 0 || confirmed)
                return null;

            // درخواست نخست هیچ پرداختی ثبت نمی‌کند؛ UI باید هشدار را نشان دهد
            // و تنها پس از تأیید صریح کاربر، همان درخواست را دوباره بفرستد.
            return new
            {
                success = false,
                confirmationRequired = true,
                message = "Payment exceeds the remaining balance.",
                balanceAmount = currentBalance,
                requestedAmount,
                excessAmount
            };
        }

        private async Task<decimal> GetGrossAmount(int studyID)
        {
            return await _context.StudyActions
                .Where(x => x.StudyID == studyID)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;
        }

        private async Task<decimal> GetNetAmount(RadiologyStudy study)
        {
            decimal gross = await GetGrossAmount(study.StudyID);
            decimal discount = CalculateDiscount(
                gross,
                study.DiscountType,
                study.DiscountValue);

            return Math.Max(0, gross - discount);
        }

        private void RecalculateDiscount(
            RadiologyStudy study,
            decimal gross)
        {
            study.DiscountAmount = CalculateDiscount(
                gross,
                study.DiscountType,
                study.DiscountValue);
            study.ModifiedDate = DateTime.Now;
        }

        private void RecordDiscountChangeIfNeeded(
            int studyID,
            RadiologyStudy study,
            decimal grossBefore,
            decimal grossAfter,
            DiscountSnapshot discountBefore)
        {
            var discountAfter = CreateDiscountSnapshot(study, grossAfter);

            if (discountBefore != discountAfter)
            {
                _financialAudit.Record(
                    User,
                    studyID,
                    "StudyDiscount",
                    studyID,
                    "Recalculated",
                    discountBefore,
                    discountAfter,
                    notes: $"Gross amount changed from {grossBefore:0} to {grossAfter:0}.");
            }
        }

        private static string? ValidateAction(StudyActionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return "Action title is required.";
            if (request.Title.Trim().Length > 200)
                return "Action title cannot exceed 200 characters.";
            if (request.Amount < 0)
                return "Action amount cannot be negative.";
            if (!IsWholeRial(request.Amount))
                return "Action amount must be a whole number of rials.";
            if (request.Description?.Length > 1000)
                return "Action description cannot exceed 1000 characters.";

            return null;
        }

        private static string? ValidatePayment(StudyPaymentRequest request)
        {
            if (request.Amount <= 0)
                return "Payment amount must be greater than zero.";
            if (!IsWholeRial(request.Amount))
                return "Payment amount must be a whole number of rials.";
            if (request.PaymentMethod is < 1 or > 3)
                return "Invalid payment method.";
            if (request.ReferenceNumber?.Length > 100)
                return "Payment reference number cannot exceed 100 characters.";
            if (request.Description?.Length > 1000)
                return "Payment description cannot exceed 1000 characters.";

            return null;
        }

        private static bool IsWholeRial(decimal amount) =>
            decimal.Truncate(amount) == amount;

        private static string? NormalizeOptionalText(string? value)
        {
            string? trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static decimal CalculateDiscount(
            decimal gross,
            byte type,
            decimal value) =>
            type == 1
                ? Math.Min(gross, decimal.Round(value, 0, MidpointRounding.AwayFromZero))
                : type == 2
                    ? Math.Min(gross, decimal.Round(gross * value / 100m, 0, MidpointRounding.AwayFromZero))
                    : 0m;

        private static ActionSnapshot CreateActionSnapshot(StudyAction item) =>
            new(item.StudyActionID, item.Title, item.Amount, item.Description);

        private static PaymentSnapshot CreatePaymentSnapshot(StudyPayment item) =>
            new(
                item.StudyPaymentID,
                item.Amount,
                item.PaymentMethod,
                item.PaymentDate,
                item.ReferenceNumber,
                item.Description,
                item.CreatedDate,
                item.ModifiedDate);

        private static DiscountSnapshot CreateDiscountSnapshot(
            RadiologyStudy study,
            decimal gross) =>
            new(
                gross,
                study.DiscountType,
                study.DiscountValue,
                study.DiscountAmount);

        private sealed record ActionSnapshot(
            int StudyActionID,
            string Title,
            decimal Amount,
            string? Description);

        private sealed record PaymentSnapshot(
            int StudyPaymentID,
            decimal Amount,
            byte PaymentMethod,
            DateTime PaymentDate,
            string? ReferenceNumber,
            string? Description,
            DateTime CreatedDate,
            DateTime? ModifiedDate);

        private sealed record DiscountSnapshot(
            decimal GrossAmount,
            byte DiscountType,
            decimal DiscountValue,
            decimal DiscountAmount);
    }
}
