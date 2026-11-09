using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;
namespace RaceDay.Mvc.Controllers;
[Authorize] public class ProfileController(ApiClient api):Controller
{
    [HttpGet] public async Task<IActionResult> Index(){var r=await api.Send<ApiUser>(HttpMethod.Get,"api/users/me");if(!r.Ok||r.Data is null){TempData["Message"]=r.Error;return View(new ProfileVm());}return View(new ProfileVm{FullName=r.Data.FullName,Email=r.Data.Email,PhoneNumber=r.Data.PhoneNumber});}
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Index(ProfileVm vm){if(!ModelState.IsValid)return View(vm);var r=await api.Send<ApiUser>(HttpMethod.Put,"api/users/me",new{vm.FullName,vm.PhoneNumber});if(!r.Ok)ModelState.AddModelError("",r.Error??"Update failed.");else TempData["Message"]="Profile updated.";return View(vm);}
}
