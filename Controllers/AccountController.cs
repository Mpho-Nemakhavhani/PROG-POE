using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;
using System.Security.Claims;
using System.Text.Json;
namespace RaceDay.Mvc.Controllers;
public class AccountController(ApiClient api):Controller
{
    [HttpGet] public IActionResult Login()=>View();
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Login(LoginVm vm)
    {
        if(!ModelState.IsValid)return View(vm);
        var r=await api.SendRaw(HttpMethod.Post,"api/auth/login",vm);
        if(!r.Ok){ModelState.AddModelError("",r.Error??"Login failed.");return View(vm);}
        var user=JsonSerializer.Deserialize<ApiUser>(r.Raw!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if(user is null){ModelState.AddModelError("","The API returned an invalid login response.");return View(vm);}
        await SignIn(user); return RedirectToAction("Index",user.Role=="Organiser"?"Organiser":"Participant");
    }
    [HttpGet] public IActionResult Register()=>View();
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Register(RegisterVm vm)
    {
        if(!ModelState.IsValid)return View(vm);
        var r=await api.SendRaw(HttpMethod.Post,"api/auth/register",vm);
        if(!r.Ok){ModelState.AddModelError("",r.Error??"Registration failed.");return View(vm);}
        var user=JsonSerializer.Deserialize<ApiUser>(r.Raw!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if(user is null){ModelState.AddModelError("","Registration response was invalid.");return View(vm);}
        await SignIn(user); return RedirectToAction("Index",user.Role=="Organiser"?"Organiser":"Participant");
    }
    private async Task SignIn(ApiUser u)
    {
        var claims=new[]{new Claim(ClaimTypes.NameIdentifier,u.UserId.ToString()),new Claim(ClaimTypes.Name,u.FullName),new Claim(ClaimTypes.Email,u.Email),new Claim(ClaimTypes.Role,u.Role)};
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
        HttpContext.Session.SetString("RaceDayUser",JsonSerializer.Serialize(u));
    }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Logout()
    {
        await api.SendNoContent(HttpMethod.Post,"api/auth/logout");
        api.ClearCookie(); await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); HttpContext.Session.Clear();
        return RedirectToAction("Index","Home");
    }
}
