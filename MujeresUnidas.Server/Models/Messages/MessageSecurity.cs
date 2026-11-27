using System.Security.Cryptography;
using System.Text;

namespace MujeresUnidas.Server.Models.Messages
{
    /// <summary>AES-256-GCM encryption at rest. The key MUST come from local server secrets, never source control.</summary>
    public sealed class MessageSecurity
    {
        #region Operation Data
        private readonly byte[] _key;
        #endregion Operation Data
        #region Initialization
        public MessageSecurity(IConfiguration settings)
        {
            try{_key=Convert.FromBase64String(settings["MU_MESSAGE_KEY_BASE64"]??string.Empty);}
            catch(FormatException){_key=Array.Empty<byte>();}
        }
        #endregion Initialization
        #region Task
        public bool IsConfigured => _key.Length==32;
        public string Encrypt(string text)
        {
            if(!IsConfigured)throw new InvalidOperationException("Message encryption key is missing or invalid.");
            byte[] plain=Encoding.UTF8.GetBytes(text);
            byte[] nonce=RandomNumberGenerator.GetBytes(12);
            byte[] cipher=new byte[plain.Length];
            byte[] tag=new byte[16];
            using(var aes=new AesGcm(_key,16))aes.Encrypt(nonce,plain,cipher,tag);
            byte[] packed=new byte[nonce.Length+tag.Length+cipher.Length];
            Buffer.BlockCopy(nonce,0,packed,0,nonce.Length);
            Buffer.BlockCopy(tag,0,packed,nonce.Length,tag.Length);
            Buffer.BlockCopy(cipher,0,packed,nonce.Length+tag.Length,cipher.Length);
            return Convert.ToBase64String(packed);
        }
        public string Decrypt(string value)
        {
            if(!IsConfigured)throw new InvalidOperationException("Message encryption key is missing or invalid.");
            byte[] packed=Convert.FromBase64String(value);
            if(packed.Length<29)throw new CryptographicException("Encrypted message format is invalid.");
            byte[] plaintext=new byte[packed.Length-28];
            using(var aes=new AesGcm(_key,16))
                aes.Decrypt(packed.AsSpan(0,12),packed.AsSpan(28),packed.AsSpan(12,16),plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        /// <summary>Binary encryption with a domain-separated key; same server secret as text chat.</summary>
        public byte[] EncryptMedia(ReadOnlySpan<byte> raw)
        {
            if(!IsConfigured)throw new InvalidOperationException("Message encryption key missing.");
            byte[] key=HMACSHA256.HashData(_key,Encoding.UTF8.GetBytes("MujeresUnidas/message-media/v1"));
            try
            {
                byte[] nonce=RandomNumberGenerator.GetBytes(12),tag=new byte[16],cipher=new byte[raw.Length];
                using(var aes=new AesGcm(key,16))aes.Encrypt(nonce,raw,cipher,tag);
                byte[] result=new byte[1+nonce.Length+tag.Length+cipher.Length];
                result[0]=1;nonce.CopyTo(result.AsSpan(1));tag.CopyTo(result.AsSpan(13));cipher.CopyTo(result.AsSpan(29));
                return result;
            }
            finally{CryptographicOperations.ZeroMemory(key);}
        }
        public byte[] DecryptMedia(ReadOnlySpan<byte> packed)
        {
            if(!IsConfigured||packed.Length<30||packed[0]!=1)throw new CryptographicException("Invalid attachment key or format.");
            byte[] key=HMACSHA256.HashData(_key,Encoding.UTF8.GetBytes("MujeresUnidas/message-media/v1"));
            try
            {
                byte[] plain=new byte[packed.Length-29];
                using(var aes=new AesGcm(key,16))aes.Decrypt(packed.Slice(1,12),packed.Slice(29),packed.Slice(13,16),plain);
                return plain;
            }
            finally{CryptographicOperations.ZeroMemory(key);}
        }
        #endregion Task
    }
}
