using System.Security.Cryptography;
namespace MujeresUnidas.Server.Models.Publications
{
    /// <summary>Private JPEG/PNG upload store, inaccessible through the web root.</summary>
    public sealed class PostMediaStore
    {
        #region Operation Data
        private readonly string _directory;
        #endregion Operation Data
        #region Initialization
        public PostMediaStore(IConfiguration configuration)
        {
            _directory=Path.GetFullPath(configuration["MU_POST_MEDIA_ROOT"]??
                Path.Combine(AppContext.BaseDirectory,"PrivateMedia","Posts"));
        }
        #endregion Initialization
        #region Structs
        public readonly record struct StoredMediaData(string Key,string ContentType);
        #endregion Structs
        #region Task
        /// <summary>Stores a bounded image under a random key. Original filenames are never retained.</summary>
        public async Task<StoredMediaData?> SaveAsync(IFormFile file)
        {
            if(file.Length is < 12 or > 2_097_152)return null;
            await using var source=file.OpenReadStream();
            byte[] head=new byte[12];
            if(await source.ReadAsync(head)!=12)return null;
            string? extension=null;
            if(head[0]==255&&head[1]==216&&head[2]==255)extension="jpg";
            else if(head.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))extension="png";
            if(extension is null)return null;
            Directory.CreateDirectory(_directory);
            string key=Convert.ToHexString(RandomNumberGenerator.GetBytes(22)).ToLowerInvariant()+"."+extension;
            await using(var destination=new FileStream(Path.Combine(_directory,key),FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                await destination.WriteAsync(head);
                await source.CopyToAsync(destination);
            }
            return new(key,extension=="png"?"image/png":"image/jpeg");
        }
        public (string Path,string ContentType)? Find(string key)
        {
            bool validType=key.EndsWith(".png",StringComparison.Ordinal) ||
                key.EndsWith(".jpg",StringComparison.Ordinal);
            if(string.IsNullOrWhiteSpace(key)||key!=Path.GetFileName(key)||
                key.Length>90||!validType)return null;
            string path=Path.Combine(_directory,key);
            if(!File.Exists(path))return null;
            return (path,key.EndsWith(".png",StringComparison.Ordinal)?"image/png":"image/jpeg");
        }
        public void Remove(string key)
        {
            var file=Find(key);
            if(file is not null)File.Delete(file.Value.Path);
        }
        #endregion Task
    }
}
