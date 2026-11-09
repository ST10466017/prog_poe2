using Microsoft.AspNetCore.Authentication.Cookies;
using RaceDay.Mvc.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => { o.IdleTimeout=TimeSpan.FromMinutes(30); o.Cookie.HttpOnly=true; o.Cookie.IsEssential=true; o.Cookie.Name=".RaceDay.Mvc.Session"; });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
 o.LoginPath="/Account/Login"; o.AccessDeniedPath="/Account/Denied"; o.Cookie.Name=".RaceDay.Mvc.Auth"; o.SlidingExpiration=true; o.ExpireTimeSpan=TimeSpan.FromMinutes(30);
});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<RaceDayApiClient>((sp,c)=> { c.BaseAddress=new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"); c.Timeout=TimeSpan.FromSeconds(30); });
var app=builder.Build();
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute(name:"default",pattern:"{controller=Home}/{action=Index}/{id?}");
app.Run();