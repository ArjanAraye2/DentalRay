namespace DentalRay.Api.Models
{
    /// <summary>How the clinic reached a patient.</summary>
    public static class PatientContactChannel
    {
        public const byte Sms = 1;
        public const byte PhoneCall = 2;
        public const byte InPerson = 3;
        public const byte Other = 4;

        public static string Name(byte channel) => channel switch
        {
            Sms => "پیامک",
            PhoneCall => "تماس تلفنی",
            InPerson => "حضوری",
            Other => "سایر",
            _ => "نامشخص"
        };
    }

    /// <summary>How a contact ended, mainly for phone calls.</summary>
    public static class PatientContactOutcome
    {
        public const byte Answered = 1;
        public const byte NoAnswer = 2;
        public const byte LeftMessage = 3;
        public const byte AskedForSms = 4;
        public const byte AppointmentBooked = 5;
        public const byte WillCallBack = 6;

        public static string Name(byte? outcome) => outcome switch
        {
            Answered => "پاسخ داد",
            NoAnswer => "پاسخ نداد",
            LeftMessage => "پیام گذاشته شد",
            AskedForSms => "درخواست پیامک کرد",
            AppointmentBooked => "نوبت گرفت",
            WillCallBack => "خودش تماس می‌گیرد",
            _ => "ثبت نشده"
        };
    }
}
