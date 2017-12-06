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

namespace AggregationService.Controllers
{
    [Produces("application/json")]
    [Route("Api")]
    public class ApiController : Controller
    {
        private const string URLArtistService = "http://localhost:61883";
        private const string URLArenaService = "http://localhost:58349";
        private const string URLConcerteService = "http://localhost:61438";

        // GET: Concerte
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
            return Ok(resultQuery);
        }

        // Delete: Concerte
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
                    return Ok();
                }
                else
                {
                    responseMessage = Encoding.UTF8.GetBytes(response.ReasonPhrase);
                    await LogQuery(request, responseString, responseMessage);
                    return BadRequest("Concerte service unavailable");
                }
            }
        }

        // Edite: Concerte
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
                        return BadRequest("Capacity lower than tickets number");
                    }
                    if (arena.City.CityName != concerteInfoFull.CityName)
                    {
                        ResponseMessage message = new ResponseMessage();
                        message.description = "This Arena is not in this city!";
                        message.message = response;
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
                    return BadRequest("Concerte Service unavailable");
                }
                request = "SERVICE: ConcerteService \r\nPUT: " + URLConcerteService + "/" + requestString + "\r\n" + client.DefaultRequestHeaders.ToString();
                responseString = response.Headers.ToString() + "\nStatus: " + response.StatusCode.ToString();
                if (response.IsSuccessStatusCode)
                {
                    responseMessage = await response.Content.ReadAsByteArrayAsync();
                    await LogQuery(request, requestMessage, responseString, responseMessage);
                    var Json = await response.Content.ReadAsStringAsync();
                    var concerte = JsonConvert.DeserializeObject<Seller>(Json);
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
                    return NoContent();
                }
            }
            else
            {
                return BadRequest("Model is invalid!");
            }
        }
    }
}