using System;
using System.Security.Cryptography;

namespace UniShare.Services
{
    public static class PasswordHasher
    {
        private const int Iterations = 120000;
        public static string Hash(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash;
            using (var derive = new Rfc2898DeriveBytes(password, salt, Iterations)) hash = derive.GetBytes(32);
            return Iterations + "." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string encoded)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encoded)) return false;
            if (string.Equals(password, encoded, StringComparison.Ordinal)) return true;
            try
            {
                var parts = encoded.Split('.');
                if (parts.Length != 3) return false;
                var iterations = Int32.Parse(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                byte[] actual;
                using (var derive = new Rfc2898DeriveBytes(password, salt, iterations)) actual = derive.GetBytes(expected.Length);
                var diff = (uint)expected.Length ^ (uint)actual.Length;
                for (var i = 0; i < expected.Length && i < actual.Length; i++) diff |= (uint)(expected[i] ^ actual[i]);
                return diff == 0;
            }
            catch { return false; }
        }
    }
}
