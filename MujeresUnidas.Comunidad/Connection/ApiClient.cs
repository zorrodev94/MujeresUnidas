using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MujeresUnidas.Comunidad.Connection
{
/// <summary>Transports community requests to the central HTTPS API without local SQL credentials.</summary>
public sealed class ApiClient
{
    // Windows: local Kestrel. Android emulator needs a trusted HTTPS host and certificate.
    private readonly HttpClient _http=new()
    {
#if ANDROID
        BaseAddress=new Uri("https://10.0.2.2:7013/"),
#else
        BaseAddress=new Uri("https://localhost:7013/"),
#endif
        Timeout=TimeSpan.FromSeconds(15)
    };
    public string? Token { get; private set; }
    public JsonElement CurrentUser { get; private set; }

    public async Task<(bool Success,JsonElement Data,string Error)> RequestAsync(HttpMethod method,string route,object? body=null)
    {
        try
        {
            using var request=new HttpRequestMessage(method,route);
            if(body is not null) request.Content=JsonContent.Create(body);
            if(!string.IsNullOrEmpty(Token)) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            string raw=await response.Content.ReadAsStringAsync();
            JsonElement json=default;
            try {if(!string.IsNullOrWhiteSpace(raw)) json=JsonSerializer.Deserialize<JsonElement>(raw);} catch(JsonException) { }
            return (response.IsSuccessStatusCode,json,response.IsSuccessStatusCode?string.Empty:ErrorMessage(json,(int)response.StatusCode));
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException)
        {
            return (false,default,"No hay conexión HTTPS con Server. Verifica su dirección y certificado de confianza.");
        }
    }
    private static string ErrorMessage(JsonElement data,int status)
    {
        if(data.ValueKind==JsonValueKind.Object)
        {
            if(data.TryGetProperty("message",out var message)) return message.GetString()??"Error";
            if(data.TryGetProperty("errors",out var errors) && errors.ValueKind==JsonValueKind.Object)
            {
                var messages=new List<string>();
                foreach(var entry in errors.EnumerateObject())
                    if(entry.Value.ValueKind==JsonValueKind.Array)
                        foreach(var item in entry.Value.EnumerateArray())
                            if(item.ValueKind==JsonValueKind.String) messages.Add(item.GetString()??"Campo inválido");
                if(messages.Count>0) return string.Join("\n",messages.Distinct().Take(3));
            }
            if(data.TryGetProperty("title",out var title)) return title.GetString()??"Revisa los datos del formulario.";
        }
        return $"No se pudo completar la solicitud (HTTP {status}).";
    }
    public void SignIn(string token,JsonElement user){Token=token;CurrentUser=user.Clone();}
    public void UpdateUser(JsonElement user){CurrentUser=user.Clone();}
    public void SignOut(){Token=null;CurrentUser=default;}
}
}
