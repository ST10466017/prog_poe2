using Microsoft.AspNetCore.Mvc;using RaceDay.Mvc.Services;using RaceDay.Mvc.Models;
namespace RaceDay.Mvc.Controllers;
public class HomeController(RaceDayApiClient api):Controller{
 public async Task<IActionResult> Index(string? eventType){try{ViewBag.EventType=eventType;var path="api/events"+(string.IsNullOrWhiteSpace(eventType)?"?status=Upcoming":"?eventType="+Uri.EscapeDataString(eventType)+"&status=Upcoming");return View(await api.Get<List<EventDto>>(path)??new());}catch(ApiException e){ViewBag.Error=e.Message;return View(new List<EventDto>());}}
 public async Task<IActionResult> Details(int id){try{ViewBag.Categories=await api.Get<List<CategoryDto>>($"api/events/{id}/categories")??new();ViewBag.Results=await api.Get<List<ResultDto>>($"api/events/{id}/results")??new();return View(await api.Get<EventDto>($"api/events/{id}"));}catch(ApiException e){TempData["Error"]=e.Message;return RedirectToAction("Index");}}
 public IActionResult Error()=>View();
}