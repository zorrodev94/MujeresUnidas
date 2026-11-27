using System.Text.Json;
using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Shared.Contracts;
namespace MujeresUnidas.Gestion.Models.Api;
/// <summary>Typed transport for WinForms Models; SQL and authorization remain in Server.</summary>
public sealed class ManagementGateway(ApiConnection api)
{
    public async Task<(bool Success,T? Value,string Error)> GetAsync<T>(string route)
    {
        var response=await api.SendAsync(HttpMethod.Get,route);
        return (response.Success,JsonProjection.Read<T>(response.Data),response.Error);
    }
    public async Task<(bool Success,string Error)> ChangeAsync(HttpMethod method,string route,object? data=null)
    {
        var response=await api.SendAsync(method,route,data);
        return (response.Success,response.Error);
    }
}
