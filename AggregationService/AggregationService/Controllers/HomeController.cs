using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AggregationService.Models;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using AggregationService.Models.ArenaService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Newtonsoft;
using System.Net.Http.Formatting;
using System.Data.Entity;
using AggregationService.Models.ModelsForView;

namespace AggregationService.Controllers
{
    public class HomeController : Controller
    {
        private const string URLArenaService = "http://localhost:58349";

        [HttpGet("{id?}")]
        public async Task<IActionResult> Index([FromRoute] int id = 1)
        {
            List<Arena> result = new List<Arena>();
            int count = 0;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string requestString = "api/arenas/page/" + id;

                HttpResponseMessage response = await client.GetAsync(requestString);
                if (response.IsSuccessStatusCode)
                {
                    var arenas = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<Arena>>(arenas);
                }
                else
                {
                    return Error();
                }

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string requestStringCount = "api/arenas/count";

                HttpResponseMessage responseStringsCount = await client.GetAsync(requestStringCount);
                if (responseStringsCount.IsSuccessStatusCode)
                {
                    var countStringsContent = await responseStringsCount.Content.ReadAsStringAsync();
                    count = JsonConvert.DeserializeObject<int>(countStringsContent);
                }
                else
                {
                    return Error();
                }
                ArenaList resultQuery = new ArenaList() { arenas = result, countArenas = count };

                return View(resultQuery);
            }
        }


        public IActionResult AddArena()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddArena([Bind("ArenaName,CityID")] Arena arena)
        {
            //СЕРИАЛИЗУЕМ arena и посылаем на ArenaService
            var values = new JObject();
            values.Add("ArenaName", arena.ArenaName);
            values.Add("CityID", arena.CityID);

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLArenaService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

            var response = await client.PostAsJsonAsync("api/arenas", values);
            if (response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));
            else
                return View("Error");
        }


        public async Task<IActionResult> Delete(int id)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string route = "api/arenas/" + id;
                HttpResponseMessage response = await client.DeleteAsync(route);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    return View("Error");
                }
            }
        }


        public IActionResult Error()
        {
            return View("Error");
        }
    }
}