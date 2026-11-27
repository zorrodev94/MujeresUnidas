using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MujeresUnidas.Gestion.Connection
{
/// <summary>Provides HTTPS communication with safe responses and descriptive API validation errors.</summary>
public sealed class ApiConnection : IDisposable
{
    // Override only by local process/system configuration, never by a URL committed to GitHub.
    private readonly HttpClient _http = new(new HttpClientHandler{AllowAutoRedirect=false})
    {
        BaseAddress = new Uri(SelectServerAddress()), Timeout=TimeSpan.FromSeconds(20)
    };
    private static string SelectServerAddress()
    {
        var setting=Environment.GetEnvironmentVariable("MU_SERVER_URL");
        if(string.IsNullOrWhiteSpace(setting))
        {
            var path=Path.Combine(AppContext.BaseDirectory,"server-endpoint.json");
            try {if(File.Exists(path)){using var doc=JsonDocument.Parse(File.ReadAllText(path));setting=doc.RootElement.GetProperty("apiBaseUrl").GetString();}}
            catch(Exception ex) when(ex is IOException or JsonException or KeyNotFoundException){System.Diagnostics.Debug.WriteLine(ex.Message);}
        }
        if(Uri.TryCreate(setting,UriKind.Absolute,out var uri) && uri.Scheme==Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Query)&&string.IsNullOrEmpty(uri.Fragment)&&uri.AbsolutePath=="/")return uri.AbsoluteUri;
        return "https://localhost:7013/";
    }
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
            return(false,default,"No se pudo conectar con Server por HTTPS. Revisa MU_SERVER_URL y el certificado.");
        }
    }
    /// <summary>Fetches only server-authorized JPEG/PNG previews for moderation.</summary>
    public async Task<byte[]?> DownloadImageAsync(string route)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,route);
            if(!string.IsNullOrEmpty(Token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>2_200_000)return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException){return null;}
    }
    /// <summary>Uploads an authenticated private profile image.</summary>
    public async Task<(bool Success,string Error)> UploadFileAsync(string route,string path)
    {
        try
        {
            await using var stream=File.OpenRead(path);
            using var form=new MultipartFormDataContent();
            using var content=new StreamContent(stream);
            content.Headers.ContentType=new MediaTypeHeaderValue(Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)?"image/png":"image/jpeg");
            form.Add(content,"file",Path.GetFileName(path));
            using var request=new HttpRequestMessage(HttpMethod.Post,route){Content=form};
            if(!string.IsNullOrEmpty(Token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            return(response.IsSuccessStatusCode,response.IsSuccessStatusCode?string.Empty:$"Error HTTP {(int)response.StatusCode} al cargar imagen.");
        }
        catch(Exception ex) when(ex is IOException or HttpRequestException or TaskCanceledException)
        {return(false,"No se pudo leer o enviar la imagen.");}
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
    public void UpdateUser(JsonElement user) => CurrentUser=user.Clone();
    public async Task SignOutAsync()
    {
        try { await SendAsync(HttpMethod.Post,"api/auth/logout"); }
        finally { SignOut(); }
    }
    public void SignOut() {Token="";CurrentUser=default;}
    public void Dispose()=>_http.Dispose();
}
}
