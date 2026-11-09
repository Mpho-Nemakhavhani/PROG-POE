using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RaceDay.Mvc.Services;

public class ApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private ISession? Session => accessor.HttpContext?.Session;
    private string? ApiCookie { get => Session?.GetString("ApiCookie"); set { if (value is not null) Session?.SetString("ApiCookie", value); else Session?.Remove("ApiCookie"); } }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(ApiCookie)) req.Headers.Add("Cookie", ApiCookie);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        return req;
    }
    public async Task<(bool Ok,string? Error,T? Data)> Send<T>(HttpMethod method,string path,object? body=null)
    {
        using var req=Request(method,path,body);
        try {
            using var res=await http.SendAsync(req);
            CaptureCookie(res);
            var raw=await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return (false, ExtractError(raw) ?? $"API request failed ({(int)res.StatusCode}).",default);
            if (typeof(T)==typeof(string)) return (true,null,(T)(object)raw);
            if (string.IsNullOrWhiteSpace(raw)) return (true,null,default);
            return (true,null,JsonSerializer.Deserialize<T>(raw,Json));
        } catch(HttpRequestException) { return (false,"Cannot connect to the RaceDay API. Start the API project and check Api:BaseUrl.",default); }
    }
    public async Task<(bool Ok,string? Error)> SendNoContent(HttpMethod method,string path,object? body=null)
    {
        var r=await Send<JsonElement>(method,path,body); return (r.Ok,r.Error);
    }
    public async Task<(bool Ok,string? Error,string? Raw)> SendRaw(HttpMethod method,string path,object? body=null)
    {
        using var req=Request(method,path,body);
        try { using var res=await http.SendAsync(req); CaptureCookie(res); var raw=await res.Content.ReadAsStringAsync(); return (res.IsSuccessStatusCode,null,raw); }
        catch(HttpRequestException){return(false,"Cannot connect to the RaceDay API.",null);}
    }
    private void CaptureCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie",out var values))
        {
            var cookie=values.FirstOrDefault(v=>v.StartsWith("RaceDay.Session=",StringComparison.OrdinalIgnoreCase));
            if(cookie is not null) ApiCookie=cookie.Split(';',2)[0];
        }
    }
    public void ClearCookie()=>ApiCookie=null;
    private static string? ExtractError(string raw)
    {
        try { using var d=JsonDocument.Parse(raw); var root=d.RootElement;
            if(root.TryGetProperty("detail",out var detail)) return detail.GetString();
            if(root.TryGetProperty("title",out var title)) return title.GetString();
            if(root.TryGetProperty("errors",out var errors)) return string.Join(" ",errors.EnumerateObject().SelectMany(x=>x.Value.EnumerateArray()).Select(x=>x.GetString()));
        } catch {}
        return string.IsNullOrWhiteSpace(raw)?null:"The request could not be completed. Check the information and try again.";
    }
}
