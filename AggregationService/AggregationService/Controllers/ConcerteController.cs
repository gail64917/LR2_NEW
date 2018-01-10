using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AggregationService.Models.ConcerteService;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using AggregationService.Models.ModelsForView;
using static AggregationService.Logger.Logger;
using AggregationService.Models.ArtistService;
using AggregationService.Models.ArenaService;
using Newtonsoft.Json.Linq;
using RestBus.RabbitMQ.Client;
using RestBus.RabbitMQ;
using RabbitModels;
using EasyNetQ;

namespace AggregationService.Controllers
{
    [Route("Concerte")]
    public class ConcerteController : Controller
    {
        private const string URLArtistService = "https://localhost:44361";
        private const string URLArenaService = "http://localhost:58349";
        private const string URLConcerteService = "http://localhost:61438";

        // GET: Concerte
        [Route("index")]
        [HttpGet("{id?}")]
        public async Task<IActionResult> Index([FromRoute] int id = 1)
        {
            List<Concerte> result = new List<Concerte>();
            List<ConcerteInfoFull> FinalResult = new List<ConcerteInfoFull>();
            int count = 0;
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/Concertes/all?valid=true&page=" + id;
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var concerts = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<Concerte>>(concerts);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);


                //
                // ПОЛУЧАЕМ КОЛ-ВО СУЩНОСТЕЙ В БД МИКРОСЕРВИСА, ЧТОБЫ УЗНАТЬ, СКОЛЬКО СТРАНИЦ РИСОВАТЬ
                //
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestStringCount = "api/Concertes/count";
                HttpResponseMessage responseStringsCount = await client.GetAsync(requestStringCount);
                request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = responseStringsCount.Headers.ToString() + "\nStatus: " + responseStringsCount.StatusCode.ToString();

                if (responseStringsCount.IsSuccessStatusCode)
                {
                    responseMessage = await responseStringsCount.Content.ReadAsByteArrayAsync();
                    var countStringsContent = await responseStringsCount.Content.ReadAsStringAsync();
                    count = JsonConvert.DeserializeObject<int>(countStringsContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(responseStringsCount.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
            }

            //
            //Вытаскиваем все Арены
            //
            List<Arena> QryArenas = new List<Arena>();
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/arenas";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var arenas = await response.Content.ReadAsStringAsync();
                    QryArenas = JsonConvert.DeserializeObject<List<Arena>>(arenas);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
            }

            //
            //Вытаскиваем всех Артистов
            //
            List<Artist> QryArtists = new List<Artist>();
            var corrId2 = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request2;
            byte[] responseMessage2;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArtistService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString2 = "api/artists";
                HttpResponseMessage response2 = await client.GetAsync(requestString2);
                request2 = "SERVICE: ArenaService \r\nGET: " + URLArtistService + "/" + requestString2 + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString2 = response2.Headers.ToString() + "\nStatus: " + response2.StatusCode.ToString();
                if (response2.IsSuccessStatusCode)
                {
                    responseMessage2 = await response2.Content.ReadAsByteArrayAsync();
                    var artists = await response2.Content.ReadAsStringAsync();
                    QryArtists = JsonConvert.DeserializeObject<List<Artist>>(artists);
                }
                else
                {
                    responseMessage2 = Encoding.UTF8.GetBytes(response2.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request2, responseString2, responseMessage2);
            }


            //СОЕДИНЯЕМ ИНФОРМАЦИЮ ИЗ 3-х сервисов
            foreach (Concerte c in result)
            {
                Arena arena = QryArenas.Where(x => x.ArenaName == c.ArenaName).FirstOrDefault();
                Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                ConcerteInfoFull concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = c.ArenaName, ArtistName = c.ArtistName, BrandName = c.Seller.BrandName, CityName = c.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                FinalResult.Add(concerteInfoFull);
            }
            ConcerteList resultQuery = new ConcerteList() { concertesInfoFull = FinalResult, countConcertes = count };
            return View(resultQuery);
        }

        // GET: Concerte/Degradation
        [Route("Degradation")]
        [HttpGet("{id?}")]
        public async Task<IActionResult> GetWithDegradation([FromRoute] int id = 1)
        {
            List<Concerte> result = new List<Concerte>();
            List<ConcerteInfoFull> FinalResult = new List<ConcerteInfoFull>();
            int count = 0;
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;


            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/Concertes";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var concerts = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<Concerte>>(concerts);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);


                //
                // ПОЛУЧАЕМ КОЛ-ВО СУЩНОСТЕЙ В БД МИКРОСЕРВИСА, ЧТОБЫ УЗНАТЬ, СКОЛЬКО СТРАНИЦ РИСОВАТЬ
                //
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestStringCount = "api/Concertes/fullCount";
                HttpResponseMessage responseStringsCount = await client.GetAsync(requestStringCount);
                request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = responseStringsCount.Headers.ToString() + "\nStatus: " + responseStringsCount.StatusCode.ToString();

                if (responseStringsCount.IsSuccessStatusCode)
                {
                    responseMessage = await responseStringsCount.Content.ReadAsByteArrayAsync();
                    var countStringsContent = await responseStringsCount.Content.ReadAsStringAsync();
                    count = JsonConvert.DeserializeObject<int>(countStringsContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(responseStringsCount.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
            }

            //
            //Вытаскиваем все Арены
            //
            List<Arena> QryArenas = new List<Arena>();
            try
            {
                QryArenas = new List<Arena>();
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(URLArenaService);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    string requestString = "api/arenas";
                    HttpResponseMessage response = await client.GetAsync(requestString);
                    request = "SERVICE: ArenaService \r\nGET: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                    string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                    if (response.IsSuccessStatusCode)
                    {
                        responseMessage = await response.Content.ReadAsByteArrayAsync();
                        var arenas = await response.Content.ReadAsStringAsync();
                        QryArenas = JsonConvert.DeserializeObject<List<Arena>>(arenas);
                    }
                    else
                    {
                        responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                        return Error();
                    }
                    await LogQuery(request, responseString, responseMessage);
                }
            }
            catch
            {

            }

            //
            //Вытаскиваем всех Артистов
            //
            List<Artist> QryArtists = new List<Artist>();
            try
            {
                QryArtists = new List<Artist>();
                var corrId2 = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                string request2;
                byte[] responseMessage2;
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(URLArtistService);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    string requestString2 = "api/artists";
                    HttpResponseMessage response2 = await client.GetAsync(requestString2);
                    request2 = "SERVICE: ArenaService \r\nGET: " + URLArtistService + "/" + requestString2 + "\r\n" + client.DefaultRequestHeaders.ToString();
                    string responseString2 = response2.Headers.ToString() + "\nStatus: " + response2.StatusCode.ToString();
                    if (response2.IsSuccessStatusCode)
                    {
                        responseMessage2 = await response2.Content.ReadAsByteArrayAsync();
                        var artists = await response2.Content.ReadAsStringAsync();
                        QryArtists = JsonConvert.DeserializeObject<List<Artist>>(artists);
                    }
                    else
                    {
                        responseMessage2 = Encoding.UTF8.GetBytes(response2.ReasonPhrase);
                        return Error();
                    }
                    await LogQuery(request2, responseString2, responseMessage2);
                }
            }
            catch
            {

            }


            //СОЕДИНЯЕМ ИНФОРМАЦИЮ ИЗ 3-х сервисов
            foreach (Concerte c in result)
            {
                try
                {
                    ConcerteInfoFull concerteInfoFull;
                    if (QryArenas.Any() && QryArtists.Any())
                    {
                        Arena arena = QryArenas.Where(x => x.ArenaName == c.ArenaName).FirstOrDefault();
                        Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                        //concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = c.ArenaName, ArtistName = c.ArtistName, BrandName = c.Seller.BrandName, CityName = c.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                        concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = arena.ArenaName, ArtistName = artist.ArtistName, BrandName = c.Seller.BrandName, CityName = arena.City.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                    }
                    else if (!QryArenas.Any() && QryArtists.Any())
                    {
                        City city = new City() { CityName = "unknown", CityPopulation = 0 };
                        Arena arena = new Arena() { ArenaName = "unknown", Capacity = 0, City = city };
                        Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                        //concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = c.ArenaName, ArtistName = c.ArtistName, BrandName = c.Seller.BrandName, CityName = c.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                        concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = arena.ArenaName, ArtistName = artist.ArtistName, BrandName = c.Seller.BrandName, CityName = arena.City.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                    }
                    else if (QryArenas.Any() && !QryArtists.Any())
                    {
                        Arena arena = QryArenas.Where(x => x.ArenaName == c.ArenaName).FirstOrDefault();
                        Artist artist = new Artist() { ArtistName = "unknow", LastFmRating = 0 };
                        //concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = c.ArenaName, ArtistName = c.ArtistName, BrandName = c.Seller.BrandName, CityName = c.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                        concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = arena.ArenaName, ArtistName = artist.ArtistName, BrandName = c.Seller.BrandName, CityName = arena.City.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                    }
                    else
                    {
                        City city = new City() { CityName = "unknown", CityPopulation = 0 };
                        Arena arena = new Arena() { ArenaName = "unknown", Capacity = 0, City = city };
                        Artist artist = new Artist() { ArtistName = "unknow", LastFmRating = 0 };
                        concerteInfoFull = new ConcerteInfoFull() { ID = c.ID, ArenaName = arena.ArenaName, ArtistName = artist.ArtistName, BrandName = c.Seller.BrandName, CityName = arena.City.CityName, Date = c.Date, Price = c.Price, ShowName = c.ShowName, TicketsNumber = c.TicketsNumber, ArenaCapacity = arena.Capacity, CityPopulation = arena.City.CityPopulation, LastFmRating = artist.LastFmRating };
                    }

                    FinalResult.Add(concerteInfoFull);
                }
       
                catch
                {

                }
            }
            ConcerteList resultQuery = new ConcerteList() { concertesInfoFull = FinalResult, countConcertes = count };
            return View(resultQuery);
        }

        [Route("Error")]
        public IActionResult Error()
        {
            return View("Error");
        }

        [HttpGet("Delete/{id?}")]
        public async Task<IActionResult> Delete(int id)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                string request;
                byte[] responseMessage;
                string route = "api/concertes/" + id;
                string requestString = route;
                HttpResponseMessage response = await client.DeleteAsync(route);
                request = "SERVICE: ConcerteService \r\nDELETE: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, responseString, responseMessage);
                    return RedirectToAction(nameof(Index), new { id = 1 });
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, responseString, responseMessage);
                    return View("Error");
                }
            }
        }

        [HttpGet("Edite/{id?}")]
        public async Task<IActionResult> Edite(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }


            //
            // ПОЛУЧАЕМ СУЩНОСТЬ с ID
            //
            Concerte concerte;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/Concertes/" + id;
                HttpResponseMessage response = await client.GetAsync(requestString);
                string request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                byte[] responseMessage;
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var concerteContent = await response.Content.ReadAsStringAsync();
                    concerte = JsonConvert.DeserializeObject<Concerte>(concerteContent);
                    if (concerte == null)
                    {
                        await LogQuery(request, responseString, responseMessage);
                        return NotFound();
                    }
                    await LogQuery(request, responseString, responseMessage);
                    ConcerteInfoFull concerteInfoFull = new ConcerteInfoFull() { ID = concerte.ID, ArtistName = concerte.ArtistName,
                                                                                        ArenaName = concerte.ArenaName, BrandName = concerte.Seller.BrandName,
                                                                                        CityName = concerte.CityName, TicketsNumber = concerte.TicketsNumber,
                                                                                        Date = concerte.Date, Price = concerte.Price, ShowName = concerte.ShowName };
                    return View(concerteInfoFull);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, responseString, responseMessage);
                    return Error();
                }
            }
        }

        [Route("Edite/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edite([Bind("ID,ShowName,TicketsNumber,Price,CityName,ArenaName,ArtistName,Date,BrandName")] ConcerteInfoFull concerteInfoFull)
        {
            if (ModelState.IsValid)
            {
                //Проверяем, валиден ли арена и артист (запрашиваем соответствующие сущности и проверяем)
                //Если валидно - СЕРИАЛИЗУЕМ concerteInfoFull и посылаем на ConcerteService
                Arena arena;
                Artist artist;
                Seller seller;

                //
                //
                //ЗАПРОС АРЕНЫ
                //
                //
                var values = new JObject();
                values.Add("Name", concerteInfoFull.ArenaName);
                var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                string request;
                string requestMessage = values.ToString();
                byte[] responseMessage;
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                string requestString = "api/Arenas/Find";
                var response = await client.PostAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "There is no ARENA with ARENA-NAME (" + concerteInfoFull.ArenaName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                    //
                    //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                    //
                }
                request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    //
                    //НАШЛИ АРЕНУ - ПРОВЕРЯЕМ ВЕРЕН ЛИ ГОРОД И ВМЕСТИТЕЛЬНОСТЬ / БИЛЕТАМ
                    //
                    var arenaJson = await response.Content.ReadAsStringAsync();
                    arena = JsonConvert.DeserializeObject<Arena>(arenaJson);
                    if (arena.Capacity < concerteInfoFull.TicketsNumber)
                    {
                        ResponseMessage message = new ResponseMessage();
                        message.description = "Arena capacity lower than tickets number!";
                        message.message = response;
                        return View("Error", message);
                    }
                    if (arena.City.CityName != concerteInfoFull.CityName)
                    {
                        ResponseMessage message = new ResponseMessage();
                        message.description = "This Arena is not in this city!";
                        message.message = response;
                        return View("Error", message);
                    }
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "ARENA NOT FOUND";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                    //
                    //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                    //
                }


                //
                //
                //ЗАПРОС АРТИСТА
                //
                //
                values = new JObject();
                values.Add("Name", concerteInfoFull.ArtistName);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArtistService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/Artists/Find";
                response = await client.PostAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "There is no ARTIST with ARTIST-NAME (" + concerteInfoFull.ArtistName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                    //
                    //НЕ НАШЛИ ARTIST С ТАКИМ NAME
                    //
                }
                request = "SERVICE: ArtistService \r\nPOST: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    //
                    //НАШЛИ ARTIST - УСПЕХ
                    //
                    var artistJson = await response.Content.ReadAsStringAsync();
                    artist = JsonConvert.DeserializeObject<Artist>(artistJson);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "ARTIST NOT FOUND";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                    //
                    //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                    //
                }

                //
                //Если ошибки не произошло - пихаем концерт в концерты
                //

                //УЗНАЕМ ПО BrandName номер SellerID или ошибку, если ошибка
                //
                //
                //ЗАПРОС Seller'a
                //
                //
                values = new JObject();
                values.Add("Name", concerteInfoFull.BrandName);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/Concertes/FindSeller";
                response = await client.PostAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "There is no Seller with BrandName (" + concerteInfoFull.BrandName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                    //
                    //НЕ НАШЛИ Seller С ТАКИМ NAME
                    //
                }
                request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    //
                    //НАШЛИ Seller - УСПЕХ
                    //
                    var sellerJson = await response.Content.ReadAsStringAsync();
                    seller = JsonConvert.DeserializeObject<Seller>(sellerJson);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Seller NOT FOUND";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                    //
                    //НЕ НАШЛИ Seller С ТАКИМ НАЗВАНИЕМ
                    //
                }

                //СЕРИАЛИЗУЕМ concerte и посылаем на ConcerteService
                values = new JObject();
                values.Add("ID", concerteInfoFull.ID);
                values.Add("ShowName", concerteInfoFull.ShowName);
                values.Add("TicketsNumber", concerteInfoFull.TicketsNumber);
                values.Add("Price", concerteInfoFull.Price);
                values.Add("CityName", concerteInfoFull.CityName);
                values.Add("ArenaName", concerteInfoFull.ArenaName);
                values.Add("ArtistName", concerteInfoFull.ArtistName);
                values.Add("Date", concerteInfoFull.Date);
                values.Add("SellerID", seller.ID);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/concertes/"+concerteInfoFull.ID;
                response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding concerte (" + concerteInfoFull.ID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ConcerteService \r\nPUT: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE CONCERTE";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            else
            {
                return View();
            }
        }

        [HttpGet("EditeAll/{id?}")]
        public async Task<IActionResult> EditeAll(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Arena arena;
            Artist artist;
            Seller seller;
            Concerte concerte;
            City city;

            ConcerteInfoFullWithId result;

            //
            //
            //Запрос КОНЦЕРТА
            //
            //
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLConcerteService);
            string requestString = "api/Concertes/"+id;
            var response = await client.GetAsync(requestString);
            if ((int)response.StatusCode == 500)
            {
                string description = "There is no Concerte with Id (" + id + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ Concerte С ТАКИМ НАЗВАНИЕМ
                //
            }
            request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, responseString, responseMessage);
                //
                //НАШЛИ Concerte
                //
                var concerteJson = await response.Content.ReadAsStringAsync();
                concerte = JsonConvert.DeserializeObject<Concerte>(concerteJson);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, responseString, responseMessage);
                string description = "Concerte NOT FOUND";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ Concerte С ТАКИМ НАЗВАНИЕМ
                //
            }

            //
            //
            //ЗАПРОС АРЕНЫ
            //
            //
            var values = new JObject();
            values.Add("Name", concerte.ArenaName);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLArenaService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/Arenas/Find";
            response = await client.PostAsJsonAsync(requestString, values);
            if ((int)response.StatusCode == 500)
            {
                string description = "There is no ARENA with ARENA-NAME (" + concerte.ArenaName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                //
            }
            request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                //
                //НАШЛИ АРЕНУ - ПРОВЕРЯЕМ ВЕРЕН ЛИ ГОРОД И ВМЕСТИТЕЛЬНОСТЬ / БИЛЕТАМ
                //
                var arenaJson = await response.Content.ReadAsStringAsync();
                arena = JsonConvert.DeserializeObject<Arena>(arenaJson);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "ARENA NOT FOUND";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                //
            }


            //
            //
            //ЗАПРОС АРТИСТА
            //
            //
            values = new JObject();
            values.Add("Name", concerte.ArtistName);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLArtistService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/Artists/Find";
            response = await client.PostAsJsonAsync(requestString, values);
            if ((int)response.StatusCode == 500)
            {
                string description = "There is no ARTIST with ARTIST-NAME (" + concerte.ArtistName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ ARTIST С ТАКИМ NAME
                //
            }
            request = "SERVICE: ArtistService \r\nPOST: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                //
                //НАШЛИ ARTIST - УСПЕХ
                //
                var artistJson = await response.Content.ReadAsStringAsync();
                artist = JsonConvert.DeserializeObject<Artist>(artistJson);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "ARTIST NOT FOUND";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ АRTIST С ТАКИМ NAME
                //
            }

            city = arena.City;
            seller = concerte.Seller;

            result = new ConcerteInfoFullWithId()
            {
                ID = concerte.ID,
                ShowName = concerte.ShowName,
                Price = concerte.Price,
                Date = concerte.Date,
                TicketsNumber = concerte.TicketsNumber,
                SellerID = seller.ID,
                BrandName = seller.BrandName,
                CityID = city.ID,
                CityName = city.CityName,
                CityPopulation = city.CityPopulation,
                ArenaID = arena.ID,
                ArenaCapacity = arena.Capacity,
                ArenaName = arena.ArenaName,
                ArtistID = artist.ID,
                ArtistName = artist.ArtistName,
                LastFmRating = artist.LastFmRating
            };

            return View(result);

        }

        [Route("EditeAll/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditeAll([Bind("ID,ShowName,TicketsNumber,Price,Date,SellerID,BrandName,CityID,CityName,CityPopulation,ArenaID,ArenaName,ArenaCapacity,ArtistID,ArtistName,LastFmRating")] ConcerteInfoFullWithId concerteInfoFullWithId)
        {
            if (ModelState.IsValid)
            {
                //СЕРИАЛИЗУЕМ Seller и посылаем на ConcerteService
                var values = new JObject();
                values.Add("ID", concerteInfoFullWithId.SellerID);
                values.Add("BrandName", concerteInfoFullWithId.BrandName);
                var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                var requestMessage = values.ToString();
                var client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                var requestString = "api/sellers/" + concerteInfoFullWithId.SellerID;
                var response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding seller (" + concerteInfoFullWithId.SellerID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                var request = "SERVICE: ConcerteService \r\nPUT: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                var responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    var responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                }
                else
                {
                    var responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE Seller";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }


                //СЕРИАЛИЗУЕМ Artist и посылаем на ArtistService
                values = new JObject();
                values.Add("ID", concerteInfoFullWithId.ArtistID);
                values.Add("ArtistName", concerteInfoFullWithId.ArtistName);
                values.Add("LastFmRating", concerteInfoFullWithId.LastFmRating);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArtistService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/artists/" + concerteInfoFullWithId.ArtistID;
                response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding an artist (" + concerteInfoFullWithId.ArtistID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArtistService \r\nPUT: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    var responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                }
                else
                {
                    var responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE ARTIST";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }


                //СЕРИАЛИЗУЕМ City и посылаем на ArenaService
                values = new JObject();
                values.Add("ID", concerteInfoFullWithId.CityID);
                values.Add("CityName", concerteInfoFullWithId.CityName);
                values.Add("CityPopulation", concerteInfoFullWithId.CityPopulation);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/cities/" + concerteInfoFullWithId.CityID;
                response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding a City (" + concerteInfoFullWithId.CityID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArenaService \r\nPUT: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    var responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                }
                else
                {
                    var responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE CITY";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }


                //СЕРИАЛИЗУЕМ Arena и посылаем на ArenaService
                values = new JObject();
                values.Add("ID", concerteInfoFullWithId.ArenaID);
                values.Add("ArenaName", concerteInfoFullWithId.ArenaName);
                values.Add("Capacity", concerteInfoFullWithId.ArenaCapacity);
                values.Add("cityId", concerteInfoFullWithId.CityID);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/arenas/" + concerteInfoFullWithId.ArenaID;
                response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding an Arena (" + concerteInfoFullWithId.ArenaID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArenaService \r\nPUT: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    var responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                }
                else
                {
                    var responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE ARENA";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }


                //СЕРИАЛИЗУЕМ concerte и посылаем на ConcerteService
                values = new JObject();
                values.Add("ID", concerteInfoFullWithId.ID);
                values.Add("ShowName", concerteInfoFullWithId.ShowName);
                values.Add("TicketsNumber", concerteInfoFullWithId.TicketsNumber);
                values.Add("Price", concerteInfoFullWithId.Price);
                values.Add("CityName", concerteInfoFullWithId.CityName);
                values.Add("ArenaName", concerteInfoFullWithId.ArenaName);
                values.Add("ArtistName", concerteInfoFullWithId.ArtistName);
                values.Add("Date", concerteInfoFullWithId.Date);
                values.Add("SellerID", concerteInfoFullWithId.SellerID);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/concertes/" + concerteInfoFullWithId.ID;
                response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding concerte (" + concerteInfoFullWithId.ID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ConcerteService \r\nPUT: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    var responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    var responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE CONCERTE";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            else
            {
                return View();
            }
        }

        [Route("AddConcerteValid")]
        public async Task<IActionResult> AddConcerteValid()
        {
            ConcerteInfoFullFake concerteFake = new ConcerteInfoFullFake();

            List<City> result = new List<City>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/cities";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<List<City>>(json);
                    concerteFake.cities = result;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
            }

            List<Seller> resultSellers = new List<Seller>();
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/sellers";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ConcerteService \r\nGET: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    resultSellers = JsonConvert.DeserializeObject<List<Seller>>(json);
                    concerteFake.brandNames = resultSellers;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
            }


            List<Arena> resultArenas = new List<Arena>();
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/arenas";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArenaService \r\nGET: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    resultArenas = JsonConvert.DeserializeObject<List<Arena>>(json);
                    concerteFake.arenas = resultArenas;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
            }

            List<Artist> resultArtists = new List<Artist>();
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(URLArtistService);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string requestString = "api/artists";
                HttpResponseMessage response = await client.GetAsync(requestString);
                request = "SERVICE: ArtistService \r\nGET: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    var json = await response.Content.ReadAsStringAsync();
                    resultArtists = JsonConvert.DeserializeObject<List<Artist>>(json);
                    concerteFake.artists = resultArtists;
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    return Error();
                }
                await LogQuery(request, responseString, responseMessage);
                //Передаем список доступных городов с ID (для дальнейшей сверки)
            }
            return View(concerteFake);
        }

        [Route("AddConcerteToAll")]
        public IActionResult AddConcerteToAll()
        {
            return View();
        }

        [Route("AddConcerteRollBack")]
        public IActionResult AddConcerteRollBack()
        {
            return View();
        }

        [Route("AddConcerteDelayed")]
        public IActionResult AddConcerteDelayed()
        {
            return View();
        }

        [Route("AddConcerteValid")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConcerteValid([Bind("BrandName, ShowName, TicketsNumber, Price, Date, CityName, ArenaName, ArtistName")] ConcerteInfoFull concerteInfoFull)
        {
            //Проверяем, валиден ли арена и артист (запрашиваем соответствующие сущности и проверяем)
            //Если валидно - СЕРИАЛИЗУЕМ concerteInfoFull и посылаем на ConcerteService
            Arena arena;
            Artist artist;
            Seller seller;
            
            //
            //
            //ЗАПРОС АРЕНЫ
            //
            //
            var values = new JObject();
            values.Add("Name", concerteInfoFull.ArenaName);
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            string requestMessage = values.ToString();
            byte[] responseMessage;
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLArenaService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            string requestString = "api/Arenas/Find";
            var response = await client.PostAsJsonAsync(requestString, values);
            if ((int)response.StatusCode == 500)
            {
                string description = "There is no ARENA with ARENA-NAME (" + concerteInfoFull.ArenaName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                //
            }
            request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                //
                //НАШЛИ АРЕНУ - ПРОВЕРЯЕМ ВЕРЕН ЛИ ГОРОД И ВМЕСТИТЕЛЬНОСТЬ / БИЛЕТАМ
                //
                var arenaJson = await response.Content.ReadAsStringAsync();
                arena = JsonConvert.DeserializeObject<Arena>(arenaJson);
                if (arena.Capacity < concerteInfoFull.TicketsNumber)
                {
                    ResponseMessage message = new ResponseMessage();
                    message.description = "Arena capacity lower than tickets number!";
                    message.message = response;
                    return View("Error", message);
                }
                if (arena.City.CityName != concerteInfoFull.CityName)
                {
                    ResponseMessage message = new ResponseMessage();
                    message.description = "This Arena is not in this city!";
                    message.message = response;
                    return View("Error", message);
                }
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "ARENA NOT FOUND";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                //
            }


            //
            //
            //ЗАПРОС АРТИСТА
            //
            //
            values = new JObject();
            values.Add("Name", concerteInfoFull.ArtistName);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLArtistService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/Artists/Find";
            response = await client.PostAsJsonAsync(requestString, values);
            if ((int)response.StatusCode == 500)
            {
                string description = "There is no ARTIST with ARTIST-NAME (" + concerteInfoFull.ArtistName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ ARTIST С ТАКИМ NAME
                //
            }
            request = "SERVICE: ArtistService \r\nPOST: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                //
                //НАШЛИ ARTIST - УСПЕХ
                //
                var artistJson = await response.Content.ReadAsStringAsync();
                artist = JsonConvert.DeserializeObject<Artist>(artistJson);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "ARTIST NOT FOUND";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ АРЕНУ С ТАКИМ НАЗВАНИЕМ
                //
            }

            //
            //Если ошибки не произошло - пихаем концерт в концерты
            //

            //УЗНАЕМ ПО BrandName номер SellerID или ошибку, если ошибка
            //
            //
            //ЗАПРОС Seller'a
            //
            //
            values = new JObject();
            values.Add("Name", concerteInfoFull.BrandName);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLConcerteService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/Concertes/FindSeller";
            response = await client.PostAsJsonAsync(requestString, values);
            if ((int)response.StatusCode == 500)
            {
                string description = "There is no Seller with BrandName (" + concerteInfoFull.BrandName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ Seller С ТАКИМ NAME
                //
            }
            request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                //
                //НАШЛИ Seller - УСПЕХ
                //
                var sellerJson = await response.Content.ReadAsStringAsync();
                seller = JsonConvert.DeserializeObject<Seller>(sellerJson);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Seller NOT FOUND";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
                //
                //НЕ НАШЛИ Seller С ТАКИМ НАЗВАНИЕМ
                //
            }

            //СЕРИАЛИЗУЕМ concerte и посылаем на ConcerteService
            values = new JObject();
            values.Add("ShowName", concerteInfoFull.ShowName);
            values.Add("TicketsNumber", concerteInfoFull.TicketsNumber);
            values.Add("Price", concerteInfoFull.Price);
            values.Add("CityName", concerteInfoFull.CityName);
            values.Add("ArenaName", concerteInfoFull.ArenaName);
            values.Add("ArtistName", concerteInfoFull.ArtistName);
            values.Add("Date", concerteInfoFull.Date);
            values.Add("SellerID", seller.ID);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLConcerteService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/concertes";
            response = await client.PostAsJsonAsync(requestString, values);
            if ((int)response.StatusCode == 500)
            {
                string description = "Error occur while adding concerte (" + concerteInfoFull.ID + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                return RedirectToAction(nameof(Index));
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "CANNOT CREATE CONCERTE";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
        }

        [Route("AddConcerteToAll")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConcerteToAll([Bind("BrandName, ShowName, TicketsNumber, Price, Date, CityName, CityPopulation, ArenaName, ArenaCapacity, ArtistName, LastFmRating")] ConcerteInfoFull concerteInfoFull)
        {
            //Пихаем все везде
            Arena arena;
            Artist artist;
            Seller seller;
            City city;
            Concerte concerte;

            //
            //Пихаем город, возвращается объект - у него берем ID и запихиваем в арену
            //
            var values = new JObject();
            values.Add("CityName", concerteInfoFull.CityName);
            values.Add("CityPopulation", concerteInfoFull.CityPopulation);
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            string requestMessage = values.ToString();
            byte[] responseMessage;
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URLArenaService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpContent content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            string requestString = "api/cities";
            var response = await client.PostAsJsonAsync("api/cities", values);
            if ((int)response.StatusCode == 500)
            {
                string description = "CANNOT ADD CITY (" + concerteInfoFull.CityName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            string responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var cityContent = await response.Content.ReadAsStringAsync();
                city = JsonConvert.DeserializeObject<City>(cityContent);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Cannot Add City";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }

            //
            //
            //В арену
            //
            //
            //СЕРИАЛИЗУЕМ arena и посылаем на ArenaService
            values = new JObject();
            values.Add("ArenaName", concerteInfoFull.ArenaName);
            values.Add("CityID", city.ID);
            values.Add("Capacity", concerteInfoFull.ArenaCapacity);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLArenaService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/arenas";
            response = await client.PostAsJsonAsync("api/arenas", values);
            if ((int)response.StatusCode == 500)
            {
                string description = "CANNOT ADD ARENA (" + concerteInfoFull.ArenaName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var arenaContent = await response.Content.ReadAsStringAsync();
                arena = JsonConvert.DeserializeObject<Arena>(arenaContent);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Cannot Add Arena";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }


            //
            //
            //В артиста
            //
            //
            values = new JObject();
            values.Add("ArtistName", concerteInfoFull.ArtistName);
            values.Add("LastFmRating", concerteInfoFull.LastFmRating);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLArtistService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/artists";
            response = await client.PostAsJsonAsync("api/artists", values);
            if ((int)response.StatusCode == 500)
            {
                string description = "CANNOT ADD ARTIST (" + concerteInfoFull.ArtistName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            request = "SERVICE: ArtistService \r\nPOST: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var artistContent = await response.Content.ReadAsStringAsync();
                artist = JsonConvert.DeserializeObject<Artist>(artistContent);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Cannot Add Artist";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }


            //
            //
            //В Seller'a
            //
            //
            values = new JObject();
            values.Add("brandName", concerteInfoFull.BrandName);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLConcerteService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/Sellers";
            response = await client.PostAsJsonAsync("api/Sellers", values);
            if ((int)response.StatusCode == 500)
            {
                string description = "CANNOT ADD Seller (" + concerteInfoFull.BrandName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var sellerContent = await response.Content.ReadAsStringAsync();
                seller = JsonConvert.DeserializeObject<Seller>(sellerContent);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Cannot Add Seller";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }


            //
            //
            //В Concert
            //
            //
            values = new JObject();
            values.Add("ShowName", concerteInfoFull.ShowName);
            values.Add("TicketsNumber", concerteInfoFull.TicketsNumber);
            values.Add("Price", concerteInfoFull.Price);
            values.Add("CityName", concerteInfoFull.CityName);
            values.Add("ArenaName", concerteInfoFull.ArenaName);
            values.Add("ArtistName", concerteInfoFull.ArtistName);
            values.Add("Date", concerteInfoFull.Date);
            values.Add("SellerID", seller.ID);
            corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            requestMessage = values.ToString();
            client = new HttpClient();
            client.BaseAddress = new Uri(URLConcerteService);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
            requestString = "api/Concertes";
            response = await client.PostAsJsonAsync("api/Concertes", values);
            if ((int)response.StatusCode == 500)
            {
                string description = "CANNOT ADD Concerte (" + concerteInfoFull.BrandName + ")";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var concerteContent = await response.Content.ReadAsStringAsync();
                concerte = JsonConvert.DeserializeObject<Concerte>(concerteContent);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "Cannot Add Concerte";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                return View("Error", message);
            }
            return RedirectToAction(nameof(Index), new { id = 1 });
        }

        [Route("AddConcerteDelayed")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConcerteDelayed([Bind("BrandName, ShowName, TicketsNumber, Price, Date, CityName, CityPopulation, ArenaName, ArenaCapacity, ArtistName, LastFmRating")] ConcerteInfoFull concerteInfoFull)
        //public async Task<IActionResult> AddConcerteDelayed([FromBody] ConcerteInfoFull concerteInfoFull)
        {
            //Пихаем все везде
            Arena arena;
            Artist artist;
            Seller seller;
            City city;
            Concerte concerte;

            var values = new JObject();
            string request;
            string requestMessage;
            byte[] responseMessage;
            System.String corrId;
            HttpClient client;
            HttpContent content;
            string requestString;
            HttpResponseMessage response;
            string responseString;

            //
            //Пихаем город, возвращается объект - у него берем ID и запихиваем в арену
            //
            try
            {

                values.Add("CityName", concerteInfoFull.CityName);
                values.Add("CityPopulation", concerteInfoFull.CityPopulation);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);

                requestMessage = values.ToString();

                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/cities";
                response = await client.PostAsJsonAsync("api/cities", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD CITY (" + concerteInfoFull.CityName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var cityContent = await response.Content.ReadAsStringAsync();
                    city = JsonConvert.DeserializeObject<City>(cityContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add City";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }

                //
                //
                //В арену
                //
                //
                //СЕРИАЛИЗУЕМ arena и посылаем на ArenaService
                values = new JObject();
                values.Add("ArenaName", concerteInfoFull.ArenaName);
                values.Add("CityID", city.ID);
                values.Add("Capacity", concerteInfoFull.ArenaCapacity);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/arenas";
                response = await client.PostAsJsonAsync("api/arenas", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD ARENA (" + concerteInfoFull.ArenaName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var arenaContent = await response.Content.ReadAsStringAsync();
                    arena = JsonConvert.DeserializeObject<Arena>(arenaContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Arena";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            catch
            {
                RabbitArenaCity rabbitArenaCity = new RabbitArenaCity() { ArenaCapacity = concerteInfoFull.ArenaCapacity, ArenaName = concerteInfoFull.ArenaName, CityName = concerteInfoFull.CityName, CityPopulation = concerteInfoFull.CityPopulation };
                var bus = RabbitHutch.CreateBus("host=localhost");
                bus.Send("arenacity", rabbitArenaCity);
            }


            //
            //
            //В артиста
            //
            //
            try
            {
                values = new JObject();
                values.Add("ArtistName", concerteInfoFull.ArtistName);
                values.Add("LastFmRating", concerteInfoFull.LastFmRating);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArtistService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/artists";
                response = await client.PostAsJsonAsync("api/artists", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD ARTIST. WILL TRY IT LATER(" + concerteInfoFull.ArtistName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    //return View("Error", message);
                }
                request = "SERVICE: ArtistService \r\nPOST: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var artistContent = await response.Content.ReadAsStringAsync();
                    artist = JsonConvert.DeserializeObject<Artist>(artistContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Artist";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            catch
            {
                RabbitArtist rabbitArtist = new RabbitArtist() { ArtistName = concerteInfoFull.ArtistName, LastFmRating = concerteInfoFull.LastFmRating };
                var bus = RabbitHutch.CreateBus("host=localhost");
                bus.Send("artist", rabbitArtist);
            }

            //
            //
            //В Seller'a
            //
            //
            try
            {
                values = new JObject();
                values.Add("brandName", concerteInfoFull.BrandName);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/Sellers";
                response = await client.PostAsJsonAsync("api/Sellers", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD Seller (" + concerteInfoFull.BrandName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var sellerContent = await response.Content.ReadAsStringAsync();
                    seller = JsonConvert.DeserializeObject<Seller>(sellerContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Seller";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }


                //
                //
                //В Concert
                //
                //
                values = new JObject();
                values.Add("ShowName", concerteInfoFull.ShowName);
                values.Add("TicketsNumber", concerteInfoFull.TicketsNumber);
                values.Add("Price", concerteInfoFull.Price);
                values.Add("CityName", concerteInfoFull.CityName);
                values.Add("ArenaName", concerteInfoFull.ArenaName);
                values.Add("ArtistName", concerteInfoFull.ArtistName);
                values.Add("Date", concerteInfoFull.Date);
                values.Add("SellerID", seller.ID);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/Concertes";
                response = await client.PostAsJsonAsync("api/Concertes", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD Concerte (" + concerteInfoFull.BrandName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var concerteContent = await response.Content.ReadAsStringAsync();
                    concerte = JsonConvert.DeserializeObject<Concerte>(concerteContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Concerte";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            catch
            {
                RabbitConcerteSeller concerteseller = new RabbitConcerteSeller() { ArenaName = concerteInfoFull.ArenaName, ArtistName = concerteInfoFull.ArtistName, BrandName = concerteInfoFull.BrandName, CityName = concerteInfoFull.CityName, Date = concerteInfoFull.Date, Price = concerteInfoFull.Price, ShowName = concerteInfoFull.ShowName, TicketsNumber = concerteInfoFull.TicketsNumber };
                var bus = RabbitHutch.CreateBus("host=localhost");
                bus.Send("concerteseller", concerteseller);
            }
            //return RedirectToAction(nameof(Index), new { id = 1 });
            return RedirectToAction(nameof(Index), "Default");
        }

        [Route("AddConcerteRollBack")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConcerteRollBack([Bind("BrandName, ShowName, TicketsNumber, Price, Date, CityName, CityPopulation, ArenaName, ArenaCapacity, ArtistName, LastFmRating")] ConcerteInfoFull concerteInfoFull)
        {
            //Пихаем все везде
            Arena arena;
            Artist artist;
            Seller seller;
            City city;
            Concerte concerte;

            var values = new JObject();
            string request;
            string requestMessage;
            byte[] responseMessage;
            System.String corrId;
            HttpClient client;
            HttpContent content;
            string requestString;
            HttpResponseMessage response;
            string responseString;

            string commentHere = "";

            //
            //Пихаем город, возвращается объект - у него берем ID и запихиваем в арену
            //
            try
            {
                values = new JObject();
                values.Add("CityName", concerteInfoFull.CityName);
                values.Add("CityPopulation", concerteInfoFull.CityPopulation);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/cities";
                response = await client.PostAsJsonAsync("api/cities", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD CITY (" + concerteInfoFull.CityName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var cityContent = await response.Content.ReadAsStringAsync();
                    city = JsonConvert.DeserializeObject<City>(cityContent);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add City";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }

                //
                //
                //В арену
                //
                //
                //СЕРИАЛИЗУЕМ arena и посылаем на ArenaService
                values = new JObject();
                values.Add("ArenaName", concerteInfoFull.ArenaName);
                values.Add("CityID", city.ID);
                values.Add("Capacity", concerteInfoFull.ArenaCapacity);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/arenas";
                response = await client.PostAsJsonAsync("api/arenas", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD ARENA (" + concerteInfoFull.ArenaName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArenaService \r\nPOST: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var arenaContent = await response.Content.ReadAsStringAsync();
                    arena = JsonConvert.DeserializeObject<Arena>(arenaContent);
                    commentHere += "Saving City\r\n";
                    commentHere += "Saving Arena\r\n";
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Arena";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            catch
            {
                ResponseMessage message = new ResponseMessage();
                message.description = "Arena Service Unavailable. Rollback!";
                //message.message = response;
                commentHere += "Arena Service Unavailable. Rollback!\r\n";
                return RedirectToAction("Comment", "Default", new { comment = commentHere });
            }


            //
            //
            //В артиста
            //
            //
            try
            {
                values = new JObject();
                values.Add("ArtistName", concerteInfoFull.ArtistName);
                values.Add("LastFmRating", concerteInfoFull.LastFmRating);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArtistService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/artists";
                response = await client.PostAsJsonAsync("api/artists", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD ARTIST (" + concerteInfoFull.ArtistName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ArtistService \r\nPOST: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var artistContent = await response.Content.ReadAsStringAsync();
                    artist = JsonConvert.DeserializeObject<Artist>(artistContent);
                    commentHere += "Saving Artist\r\n";
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Artist";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            catch
            {
                commentHere += "Artist Service Unavailable. Rollback!\r\n";
                commentHere += "Deleting Arena!\r\n";
                commentHere += "Deleting City!\r\n";
                //Сервис Артистов недоступен - удаляем арену и город, добавленные на 1 этапе
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                requestString = "api/arenas/"+arena.ID;
                response = await client.DeleteAsync(requestString);
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                request = "SERVICE: ArenaService \r\nDELETE: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                await LogQuery(request, responseString, responseMessage);

                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                requestString = "api/cities/" + city.ID;
                response = await client.DeleteAsync(requestString);
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                request = "SERVICE: ArenaService \r\nDELETE: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                await LogQuery(request, responseString, responseMessage);

                string description = "Artist Service Unavailable. Rollback!";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                //message.message = response;
                //return RedirectToAction(nameof(Index), "Default");
                
                return RedirectToAction("Comment", "Default", new { comment = commentHere });
            }


            //
            //
            //В Seller'a
            //
            //
            try
            {
                values = new JObject();
                values.Add("brandName", concerteInfoFull.BrandName);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/Sellers";
                response = await client.PostAsJsonAsync("api/Sellers", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD Seller (" + concerteInfoFull.BrandName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var sellerContent = await response.Content.ReadAsStringAsync();
                    seller = JsonConvert.DeserializeObject<Seller>(sellerContent);
                    commentHere += "Saving Seller!\r\n";
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Seller";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }


                //
                //
                //В Concert
                //
                //
                values = new JObject();
                values.Add("ShowName", concerteInfoFull.ShowName);
                values.Add("TicketsNumber", concerteInfoFull.TicketsNumber);
                values.Add("Price", concerteInfoFull.Price);
                values.Add("CityName", concerteInfoFull.CityName);
                values.Add("ArenaName", concerteInfoFull.ArenaName);
                values.Add("ArtistName", concerteInfoFull.ArtistName);
                values.Add("Date", concerteInfoFull.Date);
                values.Add("SellerID", seller.ID);
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                requestMessage = values.ToString();
                client = new HttpClient();
                client.BaseAddress = new Uri(URLConcerteService);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
                requestString = "api/Concertes";
                response = await client.PostAsJsonAsync("api/Concertes", values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "CANNOT ADD Concerte (" + concerteInfoFull.BrandName + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
                request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var concerteContent = await response.Content.ReadAsStringAsync();
                    concerte = JsonConvert.DeserializeObject<Concerte>(concerteContent);
                    commentHere += "Saving Concerte!\r\n";
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "Cannot Add Concerte";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    return View("Error", message);
                }
            }
            catch
            {
                commentHere += "Concerte Service Unavailable. Rollback!\r\n";
                commentHere += "Deleting Arena!\r\n";
                commentHere += "Deleting City!\r\n";
                commentHere += "Deleting Artist!\r\n";
                //Сервис Концертов недоступен - удаляем арену, город, артистов  добавленные на 1 этапе
                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                requestString = "api/arenas/" + arena.ID;
                response = await client.DeleteAsync(requestString);
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                request = "SERVICE: ArenaService \r\nDELETE: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                await LogQuery(request, responseString, responseMessage);

                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArenaService);
                requestString = "api/cities/" + city.ID;
                response = await client.DeleteAsync(requestString);
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                request = "SERVICE: ArenaService \r\nDELETE: " + URLArenaService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                await LogQuery(request, responseString, responseMessage);

                corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                client = new HttpClient();
                client.BaseAddress = new Uri(URLArtistService);
                requestString = "api/artists/" + artist.ID;
                response = await client.DeleteAsync(requestString);
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                request = "SERVICE: ArtistService \r\nDELETE: " + URLArtistService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                await LogQuery(request, responseString, responseMessage);

                string description = "Concerte Service Unavailable. Rollback!";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                //message.message = response;
                return RedirectToAction("Comment", "Default", new { comment = commentHere });
            }
            //return RedirectToAction(nameof(Index), new { id = 1 });
            return RedirectToAction(nameof(Index), "Default");
        }

        ////private async Task<IActionResult> SendMessageToMicroservice<T>(JObject values, string uri, string postUri, string ErrorDescription, string RequestLog,  )
        //private T SendMessageToMicroservice<T>(JObject values, string uri, string postUri, string ErrorDescription, string RequestLog)
        //{
        //    //await Task.Run(() =>
        //    //{
        //        string request;
        //        string requestMessage;
        //        byte[] responseMessage;
        //        System.String corrId;
        //        HttpClient client;
        //        HttpContent content;
        //        string requestString;
        //        HttpResponseMessage response;
        //        string responseString;

        //        corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
        //        requestMessage = values.ToString();
        //        client = new HttpClient();
        //        client.BaseAddress = new Uri(uri);
        //        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //        content = new StringContent(values.ToString(), Encoding.UTF8, "application/json");
        //        requestString = postUri;
        //        response = await client.PostAsJsonAsync(requestString, values);
        //        if ((int)response.StatusCode == 500)
        //        {
        //            string description = ErrorDescription;
        //            ResponseMessage message = new ResponseMessage();
        //            message.description = description;
        //            message.message = response;
        //            return View("Error", message);
        //        }
        //        request = RequestLog;
        //        responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
        //        if (response.IsSuccessStatusCode)
        //        {
        //            responseMessage = await response.Content.ReadAsByteArrayAsync();
        //            await LogQuery(request, requestMessage, responseString, responseMessage);
        //            var concerteContent = await response.Content.ReadAsStringAsync();
        //            T concerte = JsonConvert.DeserializeObject<T>(concerteContent);
        //        }
        //        else
        //        {
        //            responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
        //            await LogQuery(request, requestMessage, responseString, responseMessage);
        //            string description = "Cannot Add " + nameof(T).ToString();
        //            ResponseMessage message = new ResponseMessage();
        //            message.description = description;
        //            message.message = response;
        //            return View("Error", message);
        //        }
        //    //}
        //}
    }
}