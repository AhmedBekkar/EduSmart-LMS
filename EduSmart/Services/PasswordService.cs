using System;
using System.Security.Cryptography;

namespace EduSmart.Services
{
    public static class PasswordService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 10000;
        private const string Prefix = "PBKDF2";

        public static string HashPassword(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                var hash = deriveBytes.GetBytes(HashSize);
                return string.Join("$", Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
            }
        }

        public static bool VerifyPassword(string password, string storedValue)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedValue))
            {
                return false;
            }

            if (!IsHashed(storedValue))
            {
                return string.Equals(storedValue, password, StringComparison.Ordinal);
            }

            var parts = storedValue.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);

                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations))
                {
                    var actualHash = deriveBytes.GetBytes(expectedHash.Length);
                    return FixedTimeEquals(actualHash, expectedHash);
                }
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool IsHashed(string storedValue)
        {
            return storedValue != null && storedValue.StartsWith(Prefix + "$", StringComparison.Ordinal);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }
    }
}
