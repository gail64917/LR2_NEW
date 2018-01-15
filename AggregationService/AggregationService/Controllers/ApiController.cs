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
using static AggregationService.Logger.Logger;
using AggregationService.Models.ArenaService;
using AggregationService.Models.ArtistService;
using AggregationService.Models.ModelsForView;
using Newtonsoft.Json.Linq;
using RabbitModels;
using EasyNetQ;
using Microsoft.AspNetCore.Authorization;

using static RabbitModels.StatisticSender;

namespace AggregationService.Controllers
{
    [Produces("application/json")]
    [Route("Api")]
    [Authorize(Policy = "User")]
    public class ApiController : Controller
    {
        private const string URLArtistService = "https://localhost:44361";
        private const string URLArenaService = "https://localhost:44325";
        private const string URLConcerteService = "https://localhost:44381";

        [Route("test")]
        public int test()
        {
            string user = HttpContext.Session.GetString("Login");
            user = user != null ? user : "";
            SendStatistic("Api", DateTime.Now, "Test", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user);
            return 1;
        }

        // GET: api/3
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
                    string user2 = HttpContext.Session.GetString("Login");
                    user2 = user2 != null ? user2 : "";
                    SendStatistic("Api", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user2);
                    return BadRequest("Concerte Service unavailable");
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
                    string user3 = HttpContext.Session.GetString("Login");
                    user3 = user3 != null ? user3 : "";
                    SendStatistic("Api", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user3);
                    return BadRequest("Arena Service unavailable");
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
                    string user4 = HttpContext.Session.GetString("Login");
                    user4 = user4 != null ? user4 : "";
                    SendStatistic("Api", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user4);
                    return BadRequest("Artist Service unavailable");
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
            string user1 = HttpContext.Session.GetString("Login");
            user1 = user1 != null ? user1 : "";
            SendStatistic("Api", DateTime.Now, "Index", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
            return Ok(resultQuery);
        }

        // Delete: api/3
        [HttpDelete("{id?}")]
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Delete", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
                    return Ok();
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, responseString, responseMessage);
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Delete", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest("Concerte service unavailable");
                }
            }
        }

        // PUT: api/edite/3
        [Route("Edite/{id}")]
        [HttpPut]
        public async Task<IActionResult> Edite([FromBody] ConcerteInfoFull concerteInfoFull)
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest("Arena Service unavailable");
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
                        string user1 = HttpContext.Session.GetString("Login");
                        user1 = user1 != null ? user1 : "";
                        SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                        return BadRequest("Capacity lower than tickets number");
                    }
                    if (arena.City.CityName != concerteInfoFull.CityName)
                    {
                        ResponseMessage message = new ResponseMessage();
                        message.description = "This Arena is not in this city!";
                        message.message = response;
                        string user1 = HttpContext.Session.GetString("Login");
                        user1 = user1 != null ? user1 : "";
                        SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                        return BadRequest("This City does not have this Arena");
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return NoContent();
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest("Artist Service unavailable");
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return NoContent();
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest("Concerte Service unavailable");
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return NoContent();
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
                requestString = "api/concertes/" + concerteInfoFull.ID;
                response = await client.PutAsJsonAsync(requestString, values);
                if ((int)response.StatusCode == 500)
                {
                    string description = "Error occur while adding concerte (" + concerteInfoFull.ID + ")";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest("Concerte Service unavailable");
                }
                request = "SERVICE: ConcerteService \r\nPUT: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var Json = await response.Content.ReadAsStringAsync();
                    var concerte = JsonConvert.DeserializeObject<ConcerteInfoFullWithId>(Json);
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
                    return Ok(concerte);
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE CONCERTE";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return NoContent();
                }
            }
            else
            {
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Edite", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest("Model is invalid!");
            }
        }

        // PUT: api/editeall/3
        [Route("EditeAll/{id}")]
        [HttpPut]
        public async Task<IActionResult> EditeAll([FromBody] ConcerteInfoFullWithId concerteInfoFullWithId)
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
                }
                request = "SERVICE: ConcerteService \r\nPUT: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    var responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var Json = await response.Content.ReadAsStringAsync();
                    concerteInfoFullWithId = JsonConvert.DeserializeObject<ConcerteInfoFullWithId>(Json);
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
                    return Ok(concerteInfoFullWithId);
                }
                else
                {
                    var responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    string description = "CANNOT UPDATE CONCERTE";
                    ResponseMessage message = new ResponseMessage();
                    message.description = description;
                    message.message = response;
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(description);
                }
            }
            else
            {
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "EditeAll", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest();
            }
        }


        // POST: api/AddConcerteValid
        [Route("AddConcerteValid")]
        [HttpPost]
        public async Task<IActionResult> AddConcerteValid([FromBody] ConcerteInfoFull concerteInfoFull)
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
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
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(message.description);
                }
                if (arena.City.CityName != concerteInfoFull.CityName)
                {
                    ResponseMessage message = new ResponseMessage();
                    message.description = "This Arena is not in this city!";
                    message.message = response;
                    string user1 = HttpContext.Session.GetString("Login");
                    user1 = user1 != null ? user1 : "";
                    SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                    return BadRequest(message.description);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
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
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
            }
            request = "SERVICE: ConcerteService \r\nPOST: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
            responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
            if (response.IsSuccessStatusCode)
            {
                responseMessage = await response.Content.ReadAsByteArrayAsync();
                await LogQuery(request, requestMessage, responseString, responseMessage);
                var Json = await response.Content.ReadAsStringAsync();
                var concerte = JsonConvert.DeserializeObject<ConcerteInfoFullWithId>(Json);
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
                return Ok(concerte);
            }
            else
            {
                responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                await LogQuery(request, requestMessage, responseString, responseMessage);
                string description = "CANNOT CREATE CONCERTE";
                ResponseMessage message = new ResponseMessage();
                message.description = description;
                message.message = response;
                string user1 = HttpContext.Session.GetString("Login");
                user1 = user1 != null ? user1 : "";
                SendStatistic("Api", DateTime.Now, "Add Valid Concerte", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user1);
                return BadRequest(description);
            }
        }

        // POST: api/AddConcerteToAll
        [Route("AddConcerteToAll")]
        [HttpPost]
        public async Task<IActionResult> AddConcerteToAll([FromBody] ConcerteInfoFull concerteInfoFull)
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
                string user5 = HttpContext.Session.GetString("Login");
                user5 = user5 != null ? user5 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user5);
                return BadRequest(description);
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
                string user6 = HttpContext.Session.GetString("Login");
                user6 = user6 != null ? user6 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user6);
                return BadRequest(description);
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
                string user7 = HttpContext.Session.GetString("Login");
                user7 = user7 != null ? user7 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user7);
                return BadRequest(description);
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
                string user8 = HttpContext.Session.GetString("Login");
                user8 = user8 != null ? user8 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user8);
                return BadRequest(description);
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
                string user9 = HttpContext.Session.GetString("Login");
                user9 = user9 != null ? user9 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user9);
                return BadRequest(description);
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
                string user10 = HttpContext.Session.GetString("Login");
                user10 = user10 != null ? user10 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user10);
                return BadRequest(description);
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
                string user11 = HttpContext.Session.GetString("Login");
                user11 = user11 != null ? user11 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user11);
                return BadRequest(description);
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
                string user12 = HttpContext.Session.GetString("Login");
                user12 = user12 != null ? user12 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user12);
                return BadRequest(description);
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
                string user13 = HttpContext.Session.GetString("Login");
                user13 = user13 != null ? user13 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user13);
                return BadRequest(description);
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
                string user14 = HttpContext.Session.GetString("Login");
                user14 = user14 != null ? user14 : "";
                SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user14);
                return BadRequest(description);
            }
            string user1 = HttpContext.Session.GetString("Login");
            user1 = user1 != null ? user1 : "";
            SendStatistic("Api", DateTime.Now, "Add Concerte to All", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
            return Ok(concerteInfoFull);
        }

        [Route("AddConcerteDelayed")]
        [HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> AddConcerteDelayed([Bind("BrandName, ShowName, TicketsNumber, Price, Date, CityName, CityPopulation, ArenaName, ArenaCapacity, ArtistName, LastFmRating")] ConcerteInfoFull concerteInfoFull)
        public async Task<IActionResult> AddConcerteDelayed([FromBody] ConcerteInfoFull concerteInfoFull)
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

            string MethodResult = "";

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
                    string user15 = HttpContext.Session.GetString("Login");
                    user15 = user15 != null ? user15 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user15);
                    return BadRequest(description);
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
                    string user16 = HttpContext.Session.GetString("Login");
                    user16 = user16 != null ? user16 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user16);
                    return BadRequest(description);
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
                    string user17 = HttpContext.Session.GetString("Login");
                    user17 = user17 != null ? user17 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user17);
                    return BadRequest(description);
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
                    string user18 = HttpContext.Session.GetString("Login");
                    user18 = user18 != null ? user18 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user18);
                    return BadRequest(description);
                }
            }
            catch
            {
                RabbitArenaCity rabbitArenaCity = new RabbitArenaCity() { ArenaCapacity = concerteInfoFull.ArenaCapacity, ArenaName = concerteInfoFull.ArenaName, CityName = concerteInfoFull.CityName, CityPopulation = concerteInfoFull.CityPopulation };
                var bus = RabbitHutch.CreateBus("host=localhost");
                bus.Send("arenacity", rabbitArenaCity);

                MethodResult += "Arena Service does not respond. We will save all later\r\n";
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
                    string user19 = HttpContext.Session.GetString("Login");
                    user19 = user19 != null ? user19 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user19);
                    return BadRequest(description);
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
                    string user20 = HttpContext.Session.GetString("Login");
                    user20 = user20 != null ? user20 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user20);
                    return View("Error", message);
                }
            }
            catch
            {
                RabbitArtist rabbitArtist = new RabbitArtist() { ArtistName = concerteInfoFull.ArtistName, LastFmRating = concerteInfoFull.LastFmRating };
                var bus = RabbitHutch.CreateBus("host=localhost");
                bus.Send("artist", rabbitArtist);

                MethodResult += "Artist Service does not respond. We will save all later\r\n";
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
                    string user21 = HttpContext.Session.GetString("Login");
                    user21 = user21 != null ? user21 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user21);
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
                    string user22 = HttpContext.Session.GetString("Login");
                    user22 = user22 != null ? user22 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user22);
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
                    string user23 = HttpContext.Session.GetString("Login");
                    user23 = user23 != null ? user23 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user23);
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
                    string user2 = HttpContext.Session.GetString("Login");
                    user2 = user2 != null ? user2 : "";
                    SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), false, user2);
                    return View("Error", message);
                }
            }
            catch
            {
                RabbitConcerteSeller concerteseller = new RabbitConcerteSeller() { ArenaName = concerteInfoFull.ArenaName, ArtistName = concerteInfoFull.ArtistName, BrandName = concerteInfoFull.BrandName, CityName = concerteInfoFull.CityName, Date = concerteInfoFull.Date, Price = concerteInfoFull.Price, ShowName = concerteInfoFull.ShowName, TicketsNumber = concerteInfoFull.TicketsNumber };
                var bus = RabbitHutch.CreateBus("host=localhost");
                bus.Send("concerteseller", concerteseller);

                MethodResult += "Concerte Service does not respond. We will save all later\r\n";
            }
            //return RedirectToAction(nameof(Index), new { id = 1 };
            string user1 = HttpContext.Session.GetString("Login");
            user1 = user1 != null ? user1 : "";
            SendStatistic("Api", DateTime.Now, "Add Concerte Delayed", Request.HttpContext.Connection.RemoteIpAddress.ToString(), true, user1);
            return Ok(MethodResult);
        }
    }
}