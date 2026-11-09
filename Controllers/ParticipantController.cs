using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;
using System.Text.Json;
namespace RaceDay.Mvc.Controllers;
[Authorize(Roles="Participant")]
public class ParticipantController(ApiClient api):Controller
{
    public async Task<IActionResult> Index(){var r=await api.Send<List<ApiEvent>>(HttpMethod.Get,"api/events");ViewBag.Error=r.Error;return View(r.Data??[]);}
    public async Task<IActionResult> Event(int id){var r=await api.Send<ApiEvent>(HttpMethod.Get,$"api/events/{id}");if(!r.Ok||r.Data is null)return NotFound();return View(r.Data);}
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Enrol(EnrolVm vm){var r=await api.Send<JsonElement>(HttpMethod.Post,"api/enrolments",new{vm.CategoryId});TempData["Message"]=r.Ok?"Your enrolment is confirmed.":r.Error;return RedirectToAction(nameof(MyEnrolments));}
    public async Task<IActionResult> MyEnrolments(){var r=await api.Send<List<ApiEnrolment>>(HttpMethod.Get,"api/enrolments/me");ViewBag.Error=r.Error;return View(r.Data??[]);}
    public async Task<IActionResult> MyResults(){var r=await api.Send<List<ApiResult>>(HttpMethod.Get,"api/results/me");ViewBag.Error=r.Error;return View(r.Data??[]);}
}
