using System.ComponentModel.DataAnnotations;

namespace DentalRay.Api.Models
{
    // Requestهای جداگانه مانع از آن می‌شوند که Client فیلدهای سیستمی
    // مانند شناسه و زمان ایجاد را در کنار اطلاعات مالی تغییر دهد.
    public sealed class StudyActionRequest
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public sealed class StudyPaymentRequest
    {
        public decimal Amount { get; set; }
        public byte PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        // پس از هشدار سرور، کاربر باید صریحاً اضافه‌پرداخت را تأیید کند.
        public bool ConfirmOverpayment { get; set; }
    }

    public sealed class StudyDiscountRequest
    {
        public byte DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
    }
}
