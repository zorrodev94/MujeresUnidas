using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Geography;

public sealed class ZoneRepository(Database database):IZoneManager
{
    public async Task<IReadOnlyList<ServiceZone>> GetZonesAsync()
    {
        var zones=new List<ServiceZone>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand("SELECT Id,Code,DisplayName,Municipality,GeometryGeoJson,IsConceptual,IsActive FROM dbo.ServiceZones ORDER BY Code",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())zones.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.GetBoolean(5),r.GetBoolean(6)));
        return zones;
    }
    public async Task<bool> UpdateGeometryAsync(Guid zoneId,ZoneGeometryUpdate request)
    {
        if(zoneId==Guid.Empty||string.IsNullOrWhiteSpace(request.GeoJson)||request.GeoJson.Length>150000||
           string.IsNullOrWhiteSpace(request.SourceName)||request.SourceName.Length>200)return false;
        try
        {
            using var json=System.Text.Json.JsonDocument.Parse(request.GeoJson);
            if(json.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Object ||
               !json.RootElement.TryGetProperty("type",out var type) || type.GetString() is not ("Polygon" or "MultiPolygon"))return false;
        }
        catch(System.Text.Json.JsonException){return false;}
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.ServiceZones SET GeometryGeoJson=@json,GeometrySource=@source,IsConceptual=1
WHERE Id=@id",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=zoneId;
        cmd.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=request.GeoJson;
        cmd.Parameters.Add("@source",SqlDbType.NVarChar,200).Value=request.SourceName;
        return await cmd.ExecuteNonQueryAsync()==1;
    }
}
