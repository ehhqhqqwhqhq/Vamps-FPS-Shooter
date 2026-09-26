using System;
using System.Security.Cryptography;
using System.Text;

namespace Vamp.Accounts
{
    /// <summary>
    /// PBKDF2-SHA256 with a random 16-byte salt per password. Stored as "pbkdf2$sha256$iterations$salt$hash".
    /// Plaintext passwords are never stored or logged. On a real backend this runs on the server (ideally
    /// Argon2id), never on the client.
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iterations = 120000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        public static string Hash(string password)
        {
            byte[] salt = RandomBytes(SaltBytes);
            byte[] hash = Derive(password, salt, Iterations);
            return "pbkdf2$sha256$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored) || password == null) return false;
            try
            {
                string[] parts = stored.Split('$');
                if (parts.Length != 5 || parts[0] != "pbkdf2" || parts[1] != "sha256") return false;
                int iterations = int.Parse(parts[2]);
                byte[] salt = Convert.FromBase64String(parts[3]);
                byte[] expected = Convert.FromBase64String(parts[4]);
                byte[] actual = Derive(password, salt, iterations);
                return FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        public static string NewToken()
        {
            return Convert.ToBase64String(RandomBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static string Sha256(string value)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(HashBytes);
        }

        private static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
