namespace MujeresUnidas.Shared.Contracts;
public enum SiteCategory { Administrative, Psychology, Legal, CommunityActivities, MultiService }
public sealed record CareSiteDto(Guid Id,string Code,string DisplayName,string SiteType,string Municipality,
    string StateName,string AddressNote,bool IsDemo,bool IsActive,Guid? ZoneId,decimal? Latitude,decimal? Longitude);
