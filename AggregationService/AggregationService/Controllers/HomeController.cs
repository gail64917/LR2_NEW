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
using System.Threading;
using static AggregationService.Logger.Logger;

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

            /**/var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);         
            /**/string request;
            //byte[] requestMessage;
            /**/byte[] responseMessage;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                /**/string requestString = "api/arenas/page/" + id;
                HttpResponseMessage response = await client.GetAsync(requestString);
                
                /**/request = "SERVICE: ArenaService \r\nGET: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                /**/string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

                if (response.IsSuccessStatusCode)
                {
                    /**/responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var arenas = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<Arena>>(arenas);
                }
                else
                {
                    /**/responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }

                /**/await LogQuery(request, responseString, responseMessage);


                //
                // ПОЛУЧАЕМ КОЛ-ВО СУЩНОСТЕЙ В БД МИКРОСЕРВИСА, ЧТОБЫ УЗНАТЬ, СКОЛЬКО СТРАНИЦ РИСОВАТЬ
                //
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string requestStringCount = "api/arenas/count";
                HttpResponseMessage responseStringsCount = await client.GetAsync(requestStringCount);

                /**/request = "SERVICE: ArenaService \r\nGET: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                /**/responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

                if (responseStringsCount.IsSuccessStatusCode)
                {
                    /**/responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var countStringsContent = await responseStringsCount.Content.ReadAsStringAsync();
                    count = JsonConvert.DeserializeObject<int>(countStringsContent);
                }
                else
                {
                    /**/responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                ArenaList resultQuery = new ArenaList() { arenas = result, countArenas = count };

                /**/await LogQuery(request, responseString, responseMessage);

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

            /**/var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            /**/string request;
            /**/string requestMessage = values.ToString();
            /**/byte[] responseMessage;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLArenaService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");

            /**/string requestString = "api/arenas";

            var response = await client.PostAsJsonAsync("api/arenas", values);

            /**/request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            /**/string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

            if (response.IsSuccessStatusCode)
            {
                /**/responseMessage = await response.Content.ReadAsByteArrayAsync();
                /**/await LogQuery(request, requestMessage, responseString, responseMessage);
                return RedirectToAction(nameof(Index));
            }
            else
            {
                /**/responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                /**/await LogQuery(request, responseString, responseMessage);
                return View("Error");
            } 
        }


        public async Task<IActionResult> Delete(int id)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                /**/var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                /**/string request;
                //byte[] requestMessage;
                /**/byte[] responseMessage;

                string route = "api/arenas/" + id;

                /**/string requestString = route;

                HttpResponseMessage response = await client.DeleteAsync(route);

                /**/request = "SERVICE: ArenaService \r\nDELETE: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                /**/string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();

                if (response.IsSuccessStatusCode)
                {
                    /**/responseMessage = await response.Content.ReadAsByteArrayAsync();
                    /**/await LogQuery(request, responseString, responseMessage);
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    /**/responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    /**/await LogQuery(request, responseString, responseMessage);
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