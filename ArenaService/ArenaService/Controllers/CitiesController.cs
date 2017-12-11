using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArenaService.Data;
using ArenaService.Models;
using System.Text;
using ArenaService.Models.JsonBindings;
using EasyNetQ;
using System.Collections.Concurrent;
using RabbitModels;
using System.Threading;

namespace ArenaService.Controllers
{
    [Produces("application/json")]
    [Route("api/Cities")]
    public class CitiesController : Controller
    {
        private readonly ArenaContext _context;

        public CitiesController(ArenaContext context)
        {
            _context = context;
        }

        // GET: api/Cities/Secret
        [Route("Secret")]
        [HttpGet]
        public IEnumerable<City> GetCitiesSecret()
        {
            IEnumerable<City> cities = _context.Cities;

            var Bus = RabbitHutch.CreateBus("host=localhost");
            ConcurrentStack<RabbitArenaCity> arenacityCollection = new ConcurrentStack<RabbitArenaCity>();

            Bus.Receive<RabbitArenaCity>("arenacity", msg =>
            {
                arenacityCollection.Push(msg);
            });
            Thread.Sleep(5000);

            foreach (RabbitArenaCity a in arenacityCollection)
            {
                City c = new City() { CityName = a.CityName, CityPopulation = a.CityPopulation };
                _context.Cities.Add(c);
            }
            _context.SaveChanges();

            foreach (RabbitArenaCity a in arenacityCollection)
            {
                int c_id = 0;
                foreach (City c in _context.Cities)
                {
                    if (a.CityName == c.CityName)
                        c_id = c.ID;
                }
                
                Arena ar = new Arena() { ArenaName = a.ArenaName, Capacity = a.ArenaCapacity, CityID = c_id};
                _context.Arenas.Add(ar);
            }
            _context.SaveChanges();

            return cities;
        }

        // GET: api/Cities
        [HttpGet]
        public IEnumerable<City> GetCities()
        {
            IEnumerable<City> cities = _context.Cities;

            //var Bus = RabbitHutch.CreateBus("host=localhost");
            //ConcurrentStack<RabbitArenaCity> arenacityCollection = new ConcurrentStack<RabbitArenaCity>();

            //Bus.Receive<RabbitArenaCity>("arenacity", msg =>
            //{
            //    arenacityCollection.Push(msg);
            //});
            //Thread.Sleep(5000);

            //foreach (RabbitArenaCity a in arenacityCollection)
            //{
            //    City c = new City() { CityName = a.CityName, CityPopulation = a.CityPopulation };
            //    _context.Cities.Add(c);
            //}
            //_context.SaveChanges();

            //foreach (RabbitArenaCity a in arenacityCollection)
            //{
            //    int c_id = 0;
            //    foreach (City c in _context.Cities)
            //    {
            //        if (a.CityName == c.CityName)
            //            c_id = c.ID;
            //    }

            //    Arena ar = new Arena() { ArenaName = a.ArenaName, Capacity = a.ArenaCapacity, CityID = c_id };
            //    _context.Arenas.Add(ar);
            //}
            //_context.SaveChanges();

            return cities;
        }

        // GET: api/Cities/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCity([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var city = await _context.Cities.SingleOrDefaultAsync(m => m.ID == id);

            if (city == null)
            {
                return NotFound();
            }

            return Ok(city);
        }

        // PUT: api/Cities/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCity([FromRoute] int id, [FromBody] City city)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != city.ID)
            {
                return BadRequest();
            }

            _context.Entry(city).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                return Accepted(city);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CityExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }


        // POST: api/Cities
        [HttpPost]
        public async Task<IActionResult> PostCity([FromBody] City city)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Cities.Add(city);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCity", new { id = city.ID }, city);
        }


        // DELETE: api/Cities/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCity([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var city = await _context.Cities.SingleOrDefaultAsync(m => m.ID == id);
            if (city == null)
            {
                return NotFound();
            }

            _context.Cities.Remove(city);
            await _context.SaveChangesAsync();

            return Ok(city);
        }

        private bool CityExists(int id)
        {
            return _context.Cities.Any(e => e.ID == id);
        }


        // POST: api/Cities/Find
        [Route("Find")]
        [HttpPost]
        public async Task<IActionResult> FindByName([FromBody] CityBinding cityBinding)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var city = await _context.Cities.FirstOrDefaultAsync(m => m.CityName == cityBinding.Name);

            if (city == null)
            {
                return NotFound();
            }

            return Ok(city);
        }
    }
}