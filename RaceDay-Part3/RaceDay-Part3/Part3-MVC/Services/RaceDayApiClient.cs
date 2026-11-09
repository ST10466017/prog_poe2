using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
namespace RaceDay.Mvc.Services;
public class RaceDayApiClient {
 private readonly HttpClient _http; private readonly IHttpContextAccessor _accessor;
 private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=true};
 public RaceDayApiClient(HttpClient http,IHttpContextAccessor accessor){_http=http;_accessor=accessor;}
 private void AddCookie(HttpRequestMessage req){var cookie=_accessor.HttpContext?.Session.GetString("ApiCookie");if(!string.IsNullOrWhiteSpace(cookie))req.Headers.TryAddWithoutValidation("Cookie",cookie);}
 private async Task<T?> Send<T>(HttpMethod method,string path,object? body=null){
  using var req=new HttpRequestMessage(method,path); AddCookie(req);
  if(body!=null)req.Content=new StringContent(JsonSerializer.Serialize(body,Json),Encoding.UTF8,"application/json");
  using var res=await _http.SendAsync(req); var raw=await res.Content.ReadAsStringAsync();
  if(!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode,ReadError(raw));
  if(string.IsNullOrWhiteSpace(raw))return default; return JsonSerializer.Deserialize<T>(raw,Json);
 }
 private static string ReadError(string raw){try{using var d=JsonDocument.Parse(raw);if(d.RootElement.TryGetProperty("detail",out var v))return v.GetString()??"Request failed.";if(d.RootElement.TryGetProperty("title",out v))return v.GetString()??"Request failed.";if(d.RootElement.TryGetProperty("errors",out v))return string.Join(" ",v.EnumerateObject().SelectMany(x=>x.Value.EnumerateArray().Select(y=>y.GetString())));}catch{}return "The API request could not be completed.";}
 public Task<T?> Get<T>(string path)=>Send<T>(HttpMethod.Get,path);
 public Task<T?> Post<T>(string path,object body)=>Send<T>(HttpMethod.Post,path,body);
 public Task<T?> Put<T>(string path,object body)=>Send<T>(HttpMethod.Put,path,body);
 public Task<T?> Delete<T>(string path)=>Send<T>(HttpMethod.Delete,path);
 public async Task<(T? data,string? cookie)> Login<T>(string path,object body){
  using var req=new HttpRequestMessage(HttpMethod.Post,path){Content=new StringContent(JsonSerializer.Serialize(body,Json),Encoding.UTF8,"application/json")};
  using var res=await _http.SendAsync(req);var raw=await res.Content.ReadAsStringAsync();
  if(!res.IsSuccessStatusCode)throw new ApiException((int)res.StatusCode,ReadError(raw));
  var set=res.Headers.TryGetValues("Set-Cookie",out var vals)?vals.FirstOrDefault():null;
  var cookie=set?.Split(';')[0];return (JsonSerializer.Deserialize<T>(raw,Json),cookie);
 }
 public async Task Upload<T>(string path,IFormFile file){
  using var req=new HttpRequestMessage(HttpMethod.Post,path);AddCookie(req);
  var form=new MultipartFormDataContent();var stream=file.OpenReadStream();var part=new StreamContent(stream);part.Headers.ContentType=new MediaTypeHeaderValue(file.ContentType);form.Add(part,"file",file.FileName);req.Content=form;
  using var res=await _http.SendAsync(req);var raw=await res.Content.ReadAsStringAsync();if(!res.IsSuccessStatusCode)throw new ApiException((int)res.StatusCode,ReadError(raw));
 }
}
public sealed class ApiException(int status,string message):Exception(message){public int Status{get;}=status;}
