namespace DentalRay.Api.Services
{
    /// <summary>
    /// The message templates the clinic can send.
    ///
    /// Keeping the wording in one place makes it reviewable and keeps messages
    /// consistent. Placeholders are filled from the patient and the payment.
    ///
    /// Note on the POS confirmation: it deliberately contains no amount. A wrong
    /// mobile number or a shared phone would otherwise leak treatment costs, and
    /// the patient already has the terminal receipt.
    /// </summary>
    public static class MessageTemplates
    {
        public sealed record Template(string Key, string Title, string Body, bool ContainsAmount);

        public static readonly IReadOnlyList<Template> All = new[]
        {
            new Template(
                "appointment-reminder",
                "یادآوری نوبت",
                "{patient} عزیز، نوبت شما در {date} ساعت {time} ثبت شده است. Dentix",
                false),

            new Template(
                "appointment-tomorrow",
                "یادآوری نوبت فردا",
                "{patient} عزیز، یادآوری نوبت شما فردا {time} است. Dentix",
                false),

            new Template(
                "payment-confirmed",
                "تأیید پرداخت",
                "{patient} عزیز، پرداخت شما با موفقیت ثبت شد. Dentix",
                false),

            new Template(
                "images-ready",
                "آماده بودن تصاویر",
                "{patient} عزیز، تصاویر رادیولوژی شما آماده است. Dentix",
                false),

            new Template(
                "follow-up",
                "پیگیری درمان",
                "{patient} عزیز، برای پیگیری درمان لطفاً با مطب تماس بگیرید. Dentix",
                false),

            new Template(
                "balance-due",
                "یادآوری مانده حساب",
                "{patient} عزیز، مانده حساب شما {balance} تومان است. Dentix",
                true),

            new Template(
                "free",
                "متن آزاد",
                "",
                false)
        };

        public static Template? Find(string? key) =>
            string.IsNullOrWhiteSpace(key) ? null : All.FirstOrDefault(t => t.Key == key);
    }
}
