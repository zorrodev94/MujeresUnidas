namespace MujeresUnidas.Server.Models.Geography;

/// <summary>Directional *conceptual* sector, not a certified neighborhood or risk boundary.</summary>
public sealed record ServiceZone(Guid Id,string Code,string DisplayName,string Municipality,string? GeometryGeoJson,bool IsConceptual,bool IsActive);
public sealed record ZoneMetric(Guid ZoneId,Guid MetricId,int Year,int Month,decimal Value,bool IsDemonstration,string SourceNote);
public sealed record ZoneGeometryUpdate(string GeoJson,string SourceName);
public interface IZoneManager
{
    Task<IReadOnlyList<ServiceZone>> GetZonesAsync();
    Task<bool> UpdateGeometryAsync(Guid zoneId,ZoneGeometryUpdate request);
}
