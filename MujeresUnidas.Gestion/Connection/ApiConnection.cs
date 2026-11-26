using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MujeresUnidas.Gestion.Connection
{
/// <summary>Provides HTTPS communication with safe responses and descriptive API validation errors.</summary>
public sealed class ApiConnection : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://localhost:7013/"), Timeout=TimeSpan.FromSeconds(15) };
    public string Token { get; private set; } = string.Empty;
    public JsonElement CurrentUser { get; private set; }

    public async Task<(bool Success, JsonElement Data, string Error)> SendAsync(HttpMethod method,string route,object? body=null)
    {
        try
        {
            using var request=new HttpRequestMessage(method,route);
            if(body is not null) request.Content=JsonContent.Create(body);
            if(!string.IsNullOrEmpty(Token)) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            string raw=await response.Content.ReadAsStringAsync();
            JsonElement data=default;
            try {if(!string.IsNullOrWhiteSpace(raw)) data=JsonSerializer.Deserialize<JsonElement>(raw);} catch(JsonException) { }
            return(response.IsSuccessStatusCode,data,response.IsSuccessStatusCode ? "" : Explain(data,(int)response.StatusCode));
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException)
        {
            return(false,default,"No se pudo conectar con Server en HTTPS 7013. Revisa su ejecución y certificado.");
        }
    }
    private static string Explain(JsonElement data,int code)
    {
        if(data.ValueKind==JsonValueKind.Object)
        {
            if(data.TryGetProperty("message",out var msg)) return msg.GetString()??"Error";
            if(data.TryGetProperty("errors",out var errors) && errors.ValueKind==JsonValueKind.Object)
            {
                var problems=new List<string>();
                foreach(var entry in errors.EnumerateObject())
                    if(entry.Value.ValueKind==JsonValueKind.Array)
                        foreach(var detail in entry.Value.EnumerateArray())
                            if(detail.ValueKind==JsonValueKind.String) problems.Add(detail.GetString()??"Dato inválido");
                if(problems.Count>0) return string.Join("\n", problems.Distinct().Take(3));
            }
            if(data.TryGetProperty("title",out var title)) return title.GetString()??"Error de validación";
        }
        return $"La solicitud no pudo completarse (HTTP {code}).";
    }
    public void SetSession(string token,JsonElement user)
    {
        Token=token;
        CurrentUser=user.Clone();
    }
    public async Task SignOutAsync()
    {
        try { await SendAsync(HttpMethod.Post,"api/auth/logout"); }
        finally { SignOut(); }
    }
    public void SignOut() {Token="";CurrentUser=default;}
    public void Dispose()=>_http.Dispose();
}
}
