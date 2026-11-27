using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MujeresUnidas.Comunidad.Connection
{
/// <summary>Transports community requests to the central HTTPS API without local SQL credentials.</summary>
public sealed class ApiClient
{
    // End users never type network configuration. The release operator configures
    // Resources/Raw/server-endpoint.json before packaging the application.
    private const string DevelopmentUrl="https://localhost:7013/";
    private HttpClient _http;
    private readonly SemaphoreSlim _endpointLock=new(1,1);
    private bool _endpointLoaded;
    private async Task EnsureEndpointAsync()
    {
        if(_endpointLoaded)return;
        await _endpointLock.WaitAsync();
        try
        {
            if(_endpointLoaded)return;
            try
            {
                using var stream=await FileSystem.OpenAppPackageFileAsync("server-endpoint.json");
                using var document=await JsonDocument.ParseAsync(stream);
                if(document.RootElement.TryGetProperty("apiBaseUrl",out var configured))
                {
                    var url=configured.GetString();
                    if(!string.IsNullOrWhiteSpace(url))
                    {
                        // Reject insecure schemes, credentials and URI fragments.
                        if(Uri.TryCreate(url,UriKind.Absolute,out var uri) &&
                           uri.Scheme==Uri.UriSchemeHttps &&
                           string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.Query) && uri.AbsolutePath=="/")
                            ConfigureServer(url,out _);
                        else System.Diagnostics.Debug.WriteLine("Paquete: dirección HTTPS inválida.");
                    }
                }
            }
            catch(Exception ex) when(ex is IOException or JsonException or ArgumentException)
            {System.Diagnostics.Debug.WriteLine("Endpoint del paquete no configurado: "+ex.Message);}
            _endpointLoaded=true;
        }
        finally{_endpointLock.Release();}
    }
    public string ServerAddress => _http.BaseAddress?.AbsoluteUri ?? DevelopmentUrl;
    public ApiClient()
    {
        var address=DevelopmentUrl;
#if DEBUG
        address=Preferences.Default.Get("MU_API_URL",DevelopmentUrl);
#endif
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri)||
           uri.Scheme!=Uri.UriSchemeHttps)uri=new Uri(DevelopmentUrl);
        _http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=uri,Timeout=TimeSpan.FromSeconds(20)};
    }
    public bool ConfigureServer(string address,out string error)
    {
        error=string.Empty;
        if(Token is not null){error="Cierra sesión antes de cambiar de servidor.";return false;}
        if(!Uri.TryCreate(address.Trim(),UriKind.Absolute,out var uri)||
           uri.Scheme!=Uri.UriSchemeHttps||
           !string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||uri.AbsolutePath!="/")
        {error="Introduce una dirección HTTPS válida.";return false;}
        var normal=new Uri(uri.AbsoluteUri.EndsWith('/')?uri.AbsoluteUri:uri.AbsoluteUri+"/");
        var previous=_http;
        _http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=normal,Timeout=TimeSpan.FromSeconds(20)};
        previous.Dispose();
        Preferences.Default.Set("MU_API_URL",normal.AbsoluteUri);
        return true;
    }
    /// <summary>Revokes an existing session when backgrounding, even after clearing local credentials.</summary>
    public async Task SignOutAsync()
    {
        string? current=Token;
        SignOut();
        if(string.IsNullOrWhiteSpace(current))return;
        try
        {
            await EnsureEndpointAsync();
            using var request=new HttpRequestMessage(HttpMethod.Post,"api/auth/logout");
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",current);
            using var response=await _http.SendAsync(request);
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            System.Diagnostics.Debug.WriteLine("La sesión local se cerró; la revocación remota queda pendiente: "+ex.Message);
        }
    }
    public string? Token { get; private set; }
    public JsonElement CurrentUser { get; private set; }

    public async Task<(bool Success,JsonElement Data,string Error)> RequestAsync(HttpMethod method,string route,object? body=null)
    {
        try
        {
            await EnsureEndpointAsync();
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
    /// <summary>Uploads an image over the authenticated transport. Never retains a public file URL.</summary>
    public async Task<(bool Success,string Error)> UploadImageAsync(string route,FileResult file)
    {
        try
        {
            await EnsureEndpointAsync();
            using var stream=await file.OpenReadAsync();
            using var content=new MultipartFormDataContent();
            using var part=new StreamContent(stream);
            part.Headers.ContentType=new MediaTypeHeaderValue(file.ContentType is "image/png" ? "image/png" : "image/jpeg");
            content.Add(part,"file",file.FileName);
            using var request=new HttpRequestMessage(HttpMethod.Post,route){Content=content};
            if(!string.IsNullOrWhiteSpace(Token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            return (response.IsSuccessStatusCode,response.IsSuccessStatusCode?string.Empty:$"No se pudo subir la imagen (HTTP {(int)response.StatusCode}).");
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or IOException)
        {return(false,"No se pudo enviar la imagen. Comprueba la conexión.");}
    }

    public async Task<byte[]?> DownloadImageAsync(string route)
    {
        try
        {
            await EnsureEndpointAsync();
            using var request=new HttpRequestMessage(HttpMethod.Get,route);
            if(!string.IsNullOrWhiteSpace(Token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>4_500_000)return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException){return null;}
    }
    /// <summary>Private attachment upload with a strict 15 MiB cap; MIME is verified by Server.</summary>
    public async Task<(bool Success,string Error)> UploadAttachmentAsync(string route,FileResult file)
    {
        if(file is null)return(false,"Selecciona un archivo.");
        string mime=Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".png"=>"image/png", ".jpg" or ".jpeg"=>"image/jpeg",
            ".mp4"=>"video/mp4", ".mp3"=>"audio/mpeg",
            ".m4a"=>"audio/mp4", ".wav"=>"audio/wav", _=>"application/octet-stream"
        };
        try
        {
            await EnsureEndpointAsync();
            using var original=await file.OpenReadAsync();
            if(original.CanSeek && original.Length>15*1024*1024)return(false,"Archivo demasiado grande (15 MB máximo).");
            using var body=new MultipartFormDataContent();
            using var part=new StreamContent(original);
            part.Headers.ContentType=new MediaTypeHeaderValue(mime);
            body.Add(part,"file",Path.GetFileName(file.FileName));
            using var request=new HttpRequestMessage(HttpMethod.Post,route){Content=body};
            if(!string.IsNullOrEmpty(Token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request);
            return response.IsSuccessStatusCode?(true,string.Empty):
                (false,$"Adjunto rechazado (HTTP {(int)response.StatusCode}). Formato, permisos o cuota no válidos.");
        }
        catch(Exception ex) when(ex is HttpRequestException or IOException or TaskCanceledException)
        {return(false,"No se pudo enviar el archivo. Comprueba conexión y espacio disponible.");}
    }
    public async Task<(byte[]? Bytes,string Mime)> DownloadAttachmentAsync(string route)
    {
        try
        {
            await EnsureEndpointAsync();
            using var request=new HttpRequestMessage(HttpMethod.Get,route);
            if(!string.IsNullOrEmpty(Token))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>15*1024*1024)return(null,string.Empty);
            await using var stream=await response.Content.ReadAsStreamAsync();
            using var result=new MemoryStream();
            byte[] buffer=new byte[8192];int read;
            while((read=await stream.ReadAsync(buffer))>0)
            {
                if(result.Length+read>15*1024*1024)return(null,string.Empty);
                await result.WriteAsync(buffer.AsMemory(0,read));
            }
            return (result.ToArray(),response.Content.Headers.ContentType?.MediaType??"application/octet-stream");
        }
        catch(Exception ex) when(ex is HttpRequestException or IOException or TaskCanceledException)
        {System.Diagnostics.Debug.WriteLine(ex.Message);return(null,string.Empty);}
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
