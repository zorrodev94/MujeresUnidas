using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Shared.Contracts;
namespace MujeresUnidas.Comunidad.Models.Api;
/// <summary>Typed transport for MAUI Models; no direct SQL connections are possible.</summary>
public sealed class CommunityGateway(ApiClient api)
{
    public async Task<(bool Success,T? Value,string Error)> GetAsync<T>(string route)
    {
        var response=await api.RequestAsync(HttpMethod.Get,route);
        return (response.Success,JsonProjection.Read<T>(response.Data),response.Error);
    }
    public async Task<(bool Success,string Error)> ChangeAsync(HttpMethod method,string route,object? data=null)
    {
        var response=await api.RequestAsync(method,route,data);
        return (response.Success,response.Error);
    }
}
