using System.Security.Cryptography;
using System.Text;

namespace MujeresUnidas.Server.Models.User.Profiles
{
    /// <summary>Encrypts external emergency phone numbers at rest using a dedicated server secret.</summary>
    public sealed class EmergencyContactSecurity
    {
        #region Operation Data
        private readonly byte[] _key;
        #endregion

        #region Initialization
        public EmergencyContactSecurity(IConfiguration settings)
        {
            try { _key = Convert.FromBase64String(settings["MU_CONTACT_KEY_BASE64"] ?? string.Empty); }
            catch (FormatException) { _key = []; }
        }
        #endregion

        #region Security
        public bool IsConfigured => _key.Length == 32;

        public byte[] Encrypt(string value)
        {
            if (!IsConfigured) throw new InvalidOperationException("Contact encryption is not configured.");
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] plain = Encoding.UTF8.GetBytes(value);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[16];
            using var aes = new AesGcm(_key, 16);
            aes.Encrypt(nonce, plain, cipher, tag);
            return [.. nonce, .. tag, .. cipher];
        }

        public string Decrypt(byte[] packed)
        {
            if (!IsConfigured) throw new InvalidOperationException("Contact encryption is not configured.");
            if (packed.Length < 29) throw new CryptographicException("Invalid contact data.");
            byte[] plain = new byte[packed.Length - 28];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(packed.AsSpan(0, 12), packed.AsSpan(28), packed.AsSpan(12, 16), plain);
            return Encoding.UTF8.GetString(plain);
        }
        #endregion
    }
}
