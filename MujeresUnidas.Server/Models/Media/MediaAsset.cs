namespace MujeresUnidas.Server.Models.Media;

public enum MediaType { Image, Audio, Video }
public sealed record MediaAsset(Guid Id,Guid OwnerId,string ApplicationCode,MediaType MediaType,string StorageKey,string MimeType,long SizeBytes);
public sealed record MediaQuota(Guid OwnerId,string ApplicationCode,long LimitBytes,long UsedBytes);
public interface IMediaQuotaService
{
    Task<bool> TryReserveAsync(Guid ownerId,string application,long bytes);
    Task ReleaseAsync(Guid ownerId,string application,long bytes);
}
