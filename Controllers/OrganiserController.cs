using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;
namespace RaceDay.Mvc.Controllers;
[Authorize(Roles="Organiser")]
public class OrganiserController(ApiClient api):Controller
{
    public async Task<IActionResult> Index()
    {
        var r=await api.Send<List<ApiEvent>>(HttpMethod.Get,"api/events");
        var mine=(r.Data??[]).Where(e=>e.OrganiserId==int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)).ToList();
        ViewBag.Error=r.Error; return View(mine);
    }
    public async Task<IActionResult> Events(){var r=await api.Send<List<ApiEvent>>(HttpMethod.Get,"api/events");ViewBag.Error=r.Error;return View((r.Data??[]).Where(e=>e.OrganiserId==int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)).ToList());}
    [HttpGet] public IActionResult CreateEvent()=>View(new EventVm());
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> CreateEvent(EventVm vm)
    {
        if(!ModelState.IsValid)return View(vm);
        var r=await api.Send<ApiEvent>(HttpMethod.Post,"api/events",new{Name=vm.Name,vm.Description,vm.EventType,vm.EventDate,vm.Location,vm.Province});
        if(!r.Ok){ModelState.AddModelError("",r.Error??"Could not create event.");return View(vm);}
        return RedirectToAction(nameof(Events));
    }
    [HttpGet] public async Task<IActionResult> EditEvent(int id)
    {
        var r=await api.Send<ApiEvent>(HttpMethod.Get,$"api/events/{id}");
        if(!r.Ok||r.Data is null)return NotFound();
        if(r.Data.OrganiserId!=int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value))return Forbid();
        var e=r.Data;return View(new EventVm{EventId=e.EventId,Name=e.Name,Description=e.Description,EventType=e.EventType,EventDate=e.EventDate,Location=e.Location,Province=e.Province,Status=e.Status});
    }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> EditEvent(EventVm vm)
    {
        if(!ModelState.IsValid)return View(vm);
        var r=await api.Send<ApiEvent>(HttpMethod.Put,$"api/events/{vm.EventId}",new{Name=vm.Name,vm.Description,vm.EventType,vm.EventDate,vm.Location,vm.Province,vm.Status});
        if(!r.Ok){ModelState.AddModelError("",r.Error??"Could not update event.");return View(vm);}return RedirectToAction(nameof(Events));
    }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> DeleteEvent(int id){var r=await api.SendNoContent(HttpMethod.Delete,$"api/events/{id}");TempData["Message"]=r.Ok?"Event deleted.":r.Error;return RedirectToAction(nameof(Events));}
    public async Task<IActionResult> Categories(int id)
    {
        var e=await api.Send<ApiEvent>(HttpMethod.Get,$"api/events/{id}");if(!e.Ok||e.Data is null)return NotFound();
        ViewBag.Event=e.Data;var c=await api.Send<List<ApiCategory>>(HttpMethod.Get,$"api/events/{id}/categories");ViewBag.Error=c.Error;return View(c.Data??[]);
    }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> AddCategory(int id,CategoryVm vm)
    {
        if(!ModelState.IsValid)return RedirectToAction(nameof(Categories),new{id});
        var r=await api.Send<ApiCategory>(HttpMethod.Post,$"api/events/{id}/categories",vm);TempData["Message"]=r.Ok?"Category added.":r.Error;return RedirectToAction(nameof(Categories),new{id});
    }
    public async Task<IActionResult> Enrolments(int id){var r=await api.Send<List<ApiEnrolment>>(HttpMethod.Get,$"api/events/{id}/enrolments");ViewBag.Error=r.Error;ViewBag.EventId=id;return View(r.Data??[]);}
    public async Task<IActionResult> Results(int id){var r=await api.Send<List<ApiEnrolment>>(HttpMethod.Get,$"api/events/{id}/enrolments");ViewBag.Error=r.Error;ViewBag.EventId=id;return View(r.Data??[]);}
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> SaveResult(ResultVm vm)
    {
        var r=await api.Send<ApiResult>(HttpMethod.Post,"api/results",vm);TempData["Message"]=r.Ok?"Result saved.":r.Error;return RedirectToAction(nameof(Results),new{id=vm.EventId});
    }
}
