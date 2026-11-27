using System.Text.Json;
namespace MujeresUnidas.Shared.Contracts;
/// <summary>Centralized typed JSON projection; does not change business rules or authorize requests.</summary>
public static class JsonProjection
{
    private static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true};
    public static T? Read<T>(JsonElement json)
    {
        if(json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)return default;
        try {return json.Deserialize<T>(Options);}
        catch(JsonException){return default;}
    }
}
