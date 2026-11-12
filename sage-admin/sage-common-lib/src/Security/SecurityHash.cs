using System;
using System.Security.Cryptography;
using SageCommonLib.Globals;

namespace SageCommonLib.Security
{
    public class SecurityHash
    {
        /// <summary>
        /// =======================
        /// HASHED PASSWORD FORMAT
        /// =======================
        /// "v1.{iterations}.{saltBase64}.{hashBase64}"
        /// PBKDF2 with HMAC-SHA256, 128-bit salt, 256-bit subkey, 600000 iterations by default.
        /// The iteration count is stored in the hash so the policy can be raised later (see NeedsRehash).
        /// </summary>

        /// <summary>
        /// Returns a hashed representation of the specified <paramref name="_password"/>.
        /// </summary>
        /// <param name="_password">The password to generate a hash value for.</param>
        /// <param name="_iterations">PBKDF2 iteration count (lower values are for tests only).</param>
        /// <returns>The hash value as a string.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="_password" /> is null or empty.</exception>
        public static string HashPassword(string _password, int _iterations = Constants.Password.Pbkdf2Iterations)
        {
            if (string.IsNullOrEmpty(_password))
            {
                throw new ArgumentNullException(nameof(_password));
            }
            byte[] _salt = RandomNumberGenerator.GetBytes(Constants.Password.SaltSize);
            byte[] _key = Rfc2898DeriveBytes.Pbkdf2(_password, _salt, _iterations, HashAlgorithmName.SHA256, Constants.Password.SubkeySize);
            return $"v1.{_iterations}.{Convert.ToBase64String(_salt)}.{Convert.ToBase64String(_key)}";
        }

        /// <summary>
        /// Verifies that <paramref name="_providedPassword"/> matches <paramref name="_hashedPassword"/> (constant-time compare).
        /// </summary>
        /// <returns>true when the password matches; false for a wrong password or a malformed hash.</returns>
        public static bool VerifyHashedPassword(string _hashedPassword, string _providedPassword)
        {
            if (string.IsNullOrEmpty(_hashedPassword) || _providedPassword == null)
            {
                return false;
            }
            string[] _parts = _hashedPassword.Split('.');
            if (_parts.Length != 4 || _parts[0] != "v1" || !int.TryParse(_parts[1], out int _iterations) || _iterations < 1)
            {
                return false;
            }
            try
            {
                byte[] _salt = Convert.FromBase64String(_parts[2]);
                byte[] _expected = Convert.FromBase64String(_parts[3]);
                byte[] _actual = Rfc2898DeriveBytes.Pbkdf2(_providedPassword, _salt, _iterations, HashAlgorithmName.SHA256, _expected.Length);
                return CryptographicOperations.FixedTimeEquals(_actual, _expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// True when the stored hash uses fewer iterations than the current policy (rehash after a successful login).
        /// </summary>
        public static bool NeedsRehash(string _hashedPassword, int _iterations = Constants.Password.Pbkdf2Iterations)
        {
            string[] _parts = _hashedPassword.Split('.');
            return _parts.Length != 4 || !int.TryParse(_parts[1], out int _stored) || _stored < _iterations;
        }
    }
}
