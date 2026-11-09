using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.Models;
using System.Security.Claims;
namespace RaceDay.Mvc.Controllers;
public class HomeController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index()
    {
        var r=await api.Send<List<ApiEvent>>(HttpMethod.Get,"api/events");
        ViewBag.ApiError=r.Error; return View(r.Data??[]);
    }
    public IActionResult Error()=>View();
    public IActionResult AccessDenied()=>View();
}
