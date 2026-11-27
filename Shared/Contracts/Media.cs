namespace MujeresUnidas.Shared.Contracts;
public enum MediaCategory { Image, Audio, Video }
public sealed record MediaAssetDto(Guid Id,Guid OwnerId,string ApplicationCode,string MediaType,long SizeBytes,
    string MimeType,DateTime CreatedUtc);
public sealed record StorageQuotaDto(Guid AccountId,string ApplicationCode,long LimitBytes,long UsedBytes);

/// <summary>Authorized message attachment metadata; never contains a server path.</summary>
public sealed record MessageAttachmentDto(Guid AssetId,Guid MessageId,string MediaType,string MimeType,long SizeBytes);
