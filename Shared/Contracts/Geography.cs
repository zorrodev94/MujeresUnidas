namespace MujeresUnidas.Shared.Contracts;
public sealed record ServiceZoneDto(Guid Id,string Code,string DisplayName,string Municipality,
    string? GeometryGeoJson,bool IsConceptual,bool IsActive);
public sealed record ZoneMetricDto(Guid Id,Guid ZoneId,Guid MetricId,short PeriodYear,byte PeriodMonth,
    decimal ObservedValue,bool IsDemonstration,string SourceNote);
