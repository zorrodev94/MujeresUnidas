using System.Security.Cryptography;

namespace MujeresUnidas.Server.Models.User.Profiles
{
    /// <summary>Stores validated profile images outside wwwroot. No user-defined paths or HTML/SVG.</summary>
    public sealed class PrivateMediaStore(IConfiguration configuration)
    {
        #region Structs
        public readonly record struct SavedMediaData(string Key,string ContentType);
        #endregion Structs
        #region Operation Data
        private readonly string _directory=Path.GetFullPath(configuration["MU_MEDIA_ROOT"] ??
            Path.Combine(AppContext.BaseDirectory,"PrivateMedia","Profiles"));
        #endregion Operation Data
        #region Task
        public async Task<SavedMediaData?> SaveAsync(IFormFile file,bool avatar)
        {
            long limit=avatar?2L*1024*1024:4L*1024*1024;
            if(file.Length<10 || file.Length>limit)return null;
            string? suffix=null;
            await using var input=file.OpenReadStream();
            byte[] signature=new byte[12];
            if(await input.ReadAsync(signature)!=12)return null;
            if(signature[0]==0xFF && signature[1]==0xD8 && signature[2]==0xFF)suffix="jpg";
            else if(signature.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))suffix="png";
            if(suffix is null)return null; // no SVG, scripts, GIF or executable uploads
            Directory.CreateDirectory(_directory);
            string name=Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant()+"."+suffix;
            string path=Path.Combine(_directory,name);
            await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            await output.WriteAsync(signature);
            await input.CopyToAsync(output);
            return new(name,suffix=="jpg"?"image/jpeg":"image/png");
        }
        public (string Path,string ContentType)? Find(string? key)
        {
            if(string.IsNullOrWhiteSpace(key) || key!=Path.GetFileName(key) || key.Length>90 ||
               (!key.EndsWith(".png",StringComparison.Ordinal) && !key.EndsWith(".jpg",StringComparison.Ordinal)))return null;
            string path=Path.Combine(_directory,key);
            if(!File.Exists(path))return null;
            return (path,key.EndsWith(".png",StringComparison.Ordinal)?"image/png":"image/jpeg");
        }
        public void Remove(string? key)
        {
            var file=Find(key);
            if(file is not null)File.Delete(file.Value.Path);
        }
        #endregion Task
    }
}
