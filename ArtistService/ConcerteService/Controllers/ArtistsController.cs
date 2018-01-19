﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConcerteService.Data;
using ConcerteService.Models;
using ReflectionIT.Mvc.Paging;
using ArtistService.Models.JsonBindings;
using EasyNetQ;
using RabbitModels;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.AspNetCore.Authorization;

namespace ConcerteService.Controllers
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/Artists")]
    public class ArtistsController : Controller
    {
        private readonly ArtistContext _context;

        const int StringsPerPage = 10;

        public ArtistsController(ArtistContext context)
        {
            _context = context;
        }

        // GET: api/Artists
        [HttpGet]
        public IEnumerable<Artist> GetArtists()
        {
            //var Bus = RabbitHutch.CreateBus("host=localhost");
            //ConcurrentStack<Artist> artistsCollection = new ConcurrentStack<Artist>();

            //Bus.Receive<RabbitArtist>("artist", msg =>
            //{
            //    Artist artist = new Artist() { ArtistName = msg.ArtistName, LastFmRating = msg.LastFmRating };
            //    artistsCollection.Push(artist);
            //});
            //Thread.Sleep(5000);

            //foreach (Artist a in artistsCollection)
            //{
            //    _context.Add(a);
            //}
            //_context.SaveChanges();
            return _context.Artists;
        }


        // GET: api/Artists/Secret
        [Route("Secret")]
        [HttpGet]
        public IEnumerable<Artist> GetArtistsSecret()
        {
            var Bus = RabbitHutch.CreateBus("host=localhost");
            ConcurrentStack<Artist> artistsCollection = new ConcurrentStack<Artist>();

            Bus.Receive<RabbitArtist>("artist", msg =>
            {
                Artist artist = new Artist() { ArtistName = msg.ArtistName, LastFmRating = msg.LastFmRating };
                artistsCollection.Push(artist);
            });
            Thread.Sleep(5000);

            foreach (Artist a in artistsCollection)
            {
                _context.Add(a);
            }
            _context.SaveChanges();
            return _context.Artists;
        }


        // GET: api/Artists/page/{id}
        [HttpGet]
        [Route("page/{page}")]
        public List<Artist> GetArtists([FromRoute] int page = 1)
        {
            var qry = _context.Artists.OrderBy(p => p.ArtistName);

            PagingList<Artist> artistList;
            if (page != 0)
            {
                artistList = PagingList.Create(qry, StringsPerPage, page);
            }
            else
            {
                artistList = PagingList.Create(qry, _context.Artists.Count() + 1, 1);
            }

            return artistList.ToList();
        }


        // GET: api/Artists/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetArtist([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var artist = await _context.Artists.SingleOrDefaultAsync(m => m.ID == id);

            if (artist == null)
            {
                return NotFound();
            }

            return Ok(artist);
        }

        // POST: api/Artists/Find
        [Route("Find")]
        [HttpPost]
        public async Task<IActionResult> FindByName([FromBody] ArtistNameBinding name)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var artist = await _context.Artists.FirstOrDefaultAsync(m => m.ArtistName == name.Name);

            if (artist == null)
            {
                return NotFound();
            }

            return Ok(artist);
        }

        // PUT: api/Artists/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutArtist([FromRoute] int id, [FromBody] Artist artist)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != artist.ID)
            {
                return BadRequest();
            }

            _context.Entry(artist).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                return Accepted(artist);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArtistExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            //return NoContent();
        }

        // POST: api/Artists
        [HttpPost]
        public async Task<IActionResult> PostArtist([FromBody] Artist artist)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Artists.Add(artist);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetArtist", new { id = artist.ID }, artist);
        }

        // DELETE: api/Artists/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArtist([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var artist = await _context.Artists.SingleOrDefaultAsync(m => m.ID == id);
            if (artist == null)
            {
                return NotFound();
            }

            _context.Artists.Remove(artist);
            await _context.SaveChangesAsync();

            return Ok(artist);
        }

        private bool ArtistExists(int id)
        {
            return _context.Artists.Any(e => e.ID == id);
        }

        // GET: api/Artist
        [HttpGet]
        [Route("count")]
        public int GetCountArtists()
        {
            return _context.Artists.Count();
        }
    }
}