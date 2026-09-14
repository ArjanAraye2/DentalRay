using System.Security.Cryptography;

namespace DentalRay.Api.Services
{
    public sealed class PasswordService
    {
        private const int Iterations = 120_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public string HashPassword(
            string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                password);

            byte[] salt =
                RandomNumberGenerator.GetBytes(
                    SaltSize);

            byte[] hash =
                Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize);

            return string.Join(
                "$",
                "v1",
                Iterations.ToString(),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public bool VerifyPassword(
            string password,
            string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            string[] parts =
                passwordHash.Split('$');

            if (parts.Length != 4 ||
                parts[0] != "v1" ||
                !int.TryParse(parts[1], out int iterations) ||
                iterations < 10_000)
            {
                return false;
            }

            try
            {
                byte[] salt =
                    Convert.FromBase64String(parts[2]);

                byte[] expected =
                    Convert.FromBase64String(parts[3]);

                byte[] actual =
                    Rfc2898DeriveBytes.Pbkdf2(
                        password,
                        salt,
                        iterations,
                        HashAlgorithmName.SHA256,
                        expected.Length);

                return CryptographicOperations.FixedTimeEquals(
                    expected,
                    actual);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
