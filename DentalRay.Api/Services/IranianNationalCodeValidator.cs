namespace DentalRay.Api.Services
{
    /// <summary>
    /// Validates the structural/check-digit rules of a 10-digit Iranian National Code.
    /// This helper is shared by Patient and Staff/person registration/update workflows.
    /// </summary>
    public static class IranianNationalCodeValidator
    {
        public static bool IsValid(string? nationalCode)
        {
            if (string.IsNullOrWhiteSpace(nationalCode))
                return false;

            nationalCode = nationalCode.Trim();

            if (nationalCode.Length != 10 || nationalCode.Any(c => c < '0' || c > '9'))
                return false;

            // Repeated digits are not rejected by themselves. DentalRay follows
            // the requested rule: exactly 10 digits plus a valid check digit.
            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += (nationalCode[i] - '0') * (10 - i);

            int remainder = sum % 11;
            int expectedCheckDigit = remainder < 2 ? remainder : 11 - remainder;
            int actualCheckDigit = nationalCode[9] - '0';

            return actualCheckDigit == expectedCheckDigit;
        }
    }
}
