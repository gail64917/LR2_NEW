using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConcerteService.Data;
using ConcerteService.Models.Concerte;
using ReflectionIT.Mvc.Paging;
using ConcerteService.Models;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using static ConcerteService.Logger.Logger;
using ConcerteService.Models.JsonBindings;
using EasyNetQ;
using System.Collections.Concurrent;
using RabbitModels;

namespace ConcerteService.Controllers
{
    [Produces("application/json")]
    [Route("api/Concertes")]
    public class ConcertesController : Controller
    {
        private const string URLArtistService = "http://localhost:61883";
        private const string URLArenaService = "http://localhost:58349";

        const int StringsPerPage = 10;

        private readonly ConcerteContext _context;

        public ConcertesController(ConcerteContext context)
        {
            _context = context;
        }

        // GET: api/Concertes
        [HttpGet]
        public List<Concerte> GetConcertesAll()
        {
            foreach (Concerte a in _context.Concerts)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }
            return _context.Concerts.ToList();
        }

        // GET: api/Concertes/page/1
        [HttpGet]
        [Route("page/{page}")]
        public List<Concerte> GetConcertes([FromRoute] int page = 1)
        {
            var qry = _context.Concerts.OrderBy(p => p.ID);
            foreach (Concerte a in qry)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }

            PagingList<Concerte> concertesList;
            if (page != 0)
            {

                concertesList = PagingList.Create(qry, StringsPerPage, page);
            }
            else
            {
                concertesList = PagingList.Create(qry, _context.Concerts.Count() + 1, 1);
            }

            return concertesList.ToList();
        }


        // GET: api/Concertes/Valid/Secret
        [HttpGet]
        [Route("Valid/Secret")]
        public async Task<IActionResult> GetValidSecretConcertes()
        {
            var Bus = RabbitHutch.CreateBus("host=localhost");
            ConcurrentStack<RabbitConcerteSeller> concertesellerCollection = new ConcurrentStack<RabbitConcerteSeller>();

            Bus.Receive<RabbitConcerteSeller>("concerteseller", msg =>
            {
                concertesellerCollection.Push(msg);
            });
            Thread.Sleep(5000);

            foreach (RabbitConcerteSeller cs in concertesellerCollection)
            {
                Seller s = new Seller() { BrandName = cs.BrandName };
                _context.Sellers.Add(s);
            }
            _context.SaveChanges();

            foreach (RabbitConcerteSeller cs in concertesellerCollection)
            {
                int c_id = 0;
                foreach (Seller s in _context.Sellers)
                {
                    if (cs.BrandName == s.BrandName)
                        c_id = s.ID;
                }

                Concerte c = new Concerte() { ArenaName = cs.ArenaName, ArtistName = cs.ArtistName, CityName = cs.CityName, Date = cs.Date, Price = cs.Price, ShowName = cs.ShowName, TicketsNumber = cs.TicketsNumber, SellerID = c_id };
                _context.Concerts.Add(c);
            }
            _context.SaveChanges();



            var qry = _context.Concerts.OrderBy(p => p.ID);
            foreach (Concerte a in qry)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }

            //Проверить, что: 
            // 1) кол-во билетов меньше, чем вместительность арены
            // 2) город существует и корректный
            // 3) арена существует и из этого города
            // 4) артист корректный

            //
            //Вытаскиваем все Арены
            //
            List<Arena> QryArenas = new List<Arena>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
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
                }
                await LogQuery(request2, responseString2, responseMessage2);
            }

            //
            //Проверить на валидность все концерты
            //
            List<Concerte> ValidConcertes = new List<Concerte>();
            foreach(Concerte c in qry)
            {
                //находим название Арены с таким же, как в концерте
                Arena FindedArena;
                foreach(Arena a in QryArenas)
                {
                    if (a.ArenaName == c.ArenaName)
                    {
                        FindedArena = a;
                        if (a.Capacity >= c.TicketsNumber)
                        {
                            if (a.City.CityName == c.CityName)
                            {
                                Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                                if (artist != null)
                                {
                                    ValidConcertes.Add(c);
                                }
                            }
                        }
                    }
                }
            }
            return Ok(ValidConcertes);
        }

        // GET: api/Concertes
        [HttpGet]
        [Route("Valid")]
        public async Task<IActionResult> GetValidConcertes()
        {
            //var Bus = RabbitHutch.CreateBus("host=localhost");
            //ConcurrentStack<RabbitConcerteSeller> concertesellerCollection = new ConcurrentStack<RabbitConcerteSeller>();

            //Bus.Receive<RabbitConcerteSeller>("concerteseller", msg =>
            //{
            //    concertesellerCollection.Push(msg);
            //});
            //Thread.Sleep(5000);

            //foreach (RabbitConcerteSeller cs in concertesellerCollection)
            //{
            //    Seller s = new Seller() { BrandName = cs.BrandName };
            //    _context.Sellers.Add(s);
            //}
            //_context.SaveChanges();

            //foreach (RabbitConcerteSeller cs in concertesellerCollection)
            //{
            //    int c_id = 0;
            //    foreach (Seller s in _context.Sellers)
            //    {
            //        if (cs.BrandName == s.BrandName)
            //            c_id = s.ID;
            //    }

            //    Concerte c = new Concerte() { ArenaName = cs.ArenaName, ArtistName = cs.ArtistName, CityName = cs.CityName, Date = cs.Date, Price = cs.Price, ShowName = cs.ShowName, TicketsNumber = cs.TicketsNumber, SellerID = c_id };
            //    _context.Concerts.Add(c);
            //}
            //_context.SaveChanges();



            var qry = _context.Concerts.OrderBy(p => p.ID);
            foreach (Concerte a in qry)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }

            //Проверить, что: 
            // 1) кол-во билетов меньше, чем вместительность арены
            // 2) город существует и корректный
            // 3) арена существует и из этого города
            // 4) артист корректный

            //
            //Вытаскиваем все Арены
            //
            List<Arena> QryArenas = new List<Arena>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
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
                }
                await LogQuery(request2, responseString2, responseMessage2);
            }

            //
            //Проверить на валидность все концерты
            //
            List<Concerte> ValidConcertes = new List<Concerte>();
            foreach (Concerte c in qry)
            {
                //находим название Арены с таким же, как в концерте
                Arena FindedArena;
                foreach (Arena a in QryArenas)
                {
                    if (a.ArenaName == c.ArenaName)
                    {
                        FindedArena = a;
                        if (a.Capacity >= c.TicketsNumber)
                        {
                            if (a.City.CityName == c.CityName)
                            {
                                Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                                if (artist != null)
                                {
                                    ValidConcertes.Add(c);
                                }
                            }
                        }
                    }
                }
            }
            return Ok(ValidConcertes);
        }

        // GET: api/Concertes/all?Valid=1&page=1
        [HttpGet]
        [Route("All")]
        public async Task<IActionResult> GetValidConcertesPages(bool? valid=true, int page=1)
        {
            if (valid == false)
            {
                foreach (Concerte a in _context.Concerts)
                {
                    _context.Entry(a).Navigation("Seller").Load();
                }

                PagingList<Concerte> concertesList;
                if (page != 0)
                {
                    concertesList = PagingList.Create(_context.Concerts.ToList(), StringsPerPage, page);
                }
                else
                {
                    concertesList = PagingList.Create(_context.Concerts.ToList(), _context.Concerts.ToList().Count() + 1, 1);
                }

                return Ok(concertesList.ToList());
            }
            else
            {
                var qry = _context.Concerts.OrderBy(p => p.ID);
                foreach (Concerte a in qry)
                {
                    _context.Entry(a).Navigation("Seller").Load();
                }

                //
                //Вытаскиваем все Арены
                //
                List<Arena> QryArenas = new List<Arena>();
                var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
                string request;
                byte[] responseMessage;
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
                    }
                    await LogQuery(request2, responseString2, responseMessage2);
                }

                //
                //Проверить на валидность все концерты
                //
                List<Concerte> ValidConcertes = new List<Concerte>();
                foreach (Concerte c in qry)
                {
                    //находим название Арены с таким же, как в концерте
                    Arena FindedArena;
                    foreach (Arena a in QryArenas)
                    {
                        if (a.ArenaName == c.ArenaName)
                        {
                            FindedArena = a;
                            if (a.Capacity >= c.TicketsNumber)
                            {
                                if (a.City.CityName == c.CityName)
                                {
                                    Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                                    if (artist != null)
                                    {
                                        ValidConcertes.Add(c);
                                    }
                                }
                            }
                        }
                    }
                }

                PagingList<Concerte> concertesList;
                if (page != 0)
                {
                    concertesList = PagingList.Create(ValidConcertes, StringsPerPage, page);
                }
                else
                {
                    concertesList = PagingList.Create(ValidConcertes, ValidConcertes.Count() + 1, 1);
                }

                return Ok(concertesList.ToList());
            }
        }

        // GET: api/Concertes/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetConcerte([FromRoute] int id)
        {
            var qry = _context.Concerts.OrderBy(p => p.ID);
            foreach (Concerte a in qry)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var concerte = await _context.Concerts.SingleOrDefaultAsync(m => m.ID == id);

            if (concerte == null)
            {
                return NotFound();
            }

            return Ok(concerte);
        }

        // PUT: api/Concertes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutConcerte([FromRoute] int id, [FromBody] Concerte concerte)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != concerte.ID)
            {
                return BadRequest();
            }

            _context.Entry(concerte).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                return Accepted(concerte);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ConcerteExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // POST: api/Concertes
        [HttpPost]
        public async Task<IActionResult> PostConcerte([FromBody] Concerte concerte)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Concerts.Add(concerte);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetConcerte", new { id = concerte.ID }, concerte);
        }

        // DELETE: api/Concertes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConcerte([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var concerte = await _context.Concerts.SingleOrDefaultAsync(m => m.ID == id);

            _context.Entry(concerte).Navigation("Seller").Load();

            if (concerte == null)
            {
                return NotFound();
            }

            _context.Concerts.Remove(concerte);
            await _context.SaveChangesAsync();

            return Ok(concerte);
        }

        private bool ConcerteExists(int id)
        {
            return _context.Concerts.Any(e => e.ID == id);
        }

        // GET: api/Concertes
        [HttpGet]
        [Route("count")]
        public async Task<IActionResult> GetCountArtists()
        {
            var qry = _context.Concerts.OrderBy(p => p.ID);
            foreach (Concerte a in qry)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }

            //Проверить, что: 
            // 1) кол-во билетов меньше, чем вместительность арены
            // 2) город существует и корректный
            // 3) арена существует и из этого города
            // 4) артист корректный

            //
            //Вытаскиваем все Арены
            //
            List<Arena> QryArenas = new List<Arena>();
            var corrId = string.Format("{0}{1}", DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);
            string request;
            byte[] responseMessage;
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
                }
                await LogQuery(request2, responseString2, responseMessage2);
            }

            //
            //Проверить на валидность все концерты
            //
            List<Concerte> ValidConcertes = new List<Concerte>();
            foreach (Concerte c in qry)
            {
                //находим название Арены с таким же, как в концерте
                Arena FindedArena;
                foreach (Arena a in QryArenas)
                {
                    if (a.ArenaName == c.ArenaName)
                    {
                        FindedArena = a;
                        if (a.Capacity >= c.TicketsNumber)
                        {
                            if (a.City.CityName == c.CityName)
                            {
                                Artist artist = QryArtists.Where(x => x.ArtistName == c.ArtistName).FirstOrDefault();
                                if (artist != null)
                                {
                                    ValidConcertes.Add(c);
                                }
                            }
                        }
                    }
                }
            }
            return Ok(ValidConcertes.Count());
        }


        // GET: api/Concertes/fullCount
        [HttpGet]
        [Route("fullCount")]
        public async Task<IActionResult> GetFullCountArtists()
        {
            var qry = _context.Concerts.OrderBy(p => p.ID);
            foreach (Concerte a in qry)
            {
                _context.Entry(a).Navigation("Seller").Load();
            }

            
            return Ok(qry.Count());
        }


        // POST: api/Concerte/FindSeller
        [Route("FindSeller")]
        [HttpPost]
        public async Task<IActionResult> FindByName([FromBody] SellerNameBinding name)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var seller = await _context.Sellers.FirstOrDefaultAsync(m => m.BrandName == name.Name);

            if (seller == null)
            {
                return NotFound();
            }

            return Ok(seller);
        }

        // POST: api/Concertes/Find
        [Route("Find")]
        [HttpPost]
        public async Task<IActionResult> FindConcerte([FromBody] ShowNameBinding name)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var show = await _context.Concerts.FirstOrDefaultAsync(m => m.ShowName == name.Name);

            if (show == null)
            {
                return NotFound();
            }

            return Ok(show);
        }
    }
}