using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConcerteService.Data;
using ConcerteService.Models.Concerte;
using ConcerteService.Models.JsonBindings;

namespace ConcerteService.Controllers
{
    [Produces("application/json")]
    [Route("api/Sellers")]
    public class SellersController : Controller
    {
        private readonly ConcerteContext _context;

        public SellersController(ConcerteContext context)
        {
            _context = context;
        }

        // GET: api/Sellers
        [HttpGet]
        public IEnumerable<Seller> GetSellers()
        {
            return _context.Sellers;
        }

        // GET: api/Sellers/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSeller([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var seller = await _context.Sellers.SingleOrDefaultAsync(m => m.ID == id);

            if (seller == null)
            {
                return NotFound();
            }

            return Ok(seller);
        }

        // PUT: api/Sellers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSeller([FromRoute] int id, [FromBody] Seller seller)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != seller.ID)
            {
                return BadRequest();
            }

            _context.Entry(seller).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                return Accepted(seller);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SellerExists(id))
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

        // POST: api/Sellers
        [HttpPost]
        public async Task<IActionResult> PostSeller([FromBody] Seller seller)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Sellers.Add(seller);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSeller", new { id = seller.ID }, seller);
        }

        // DELETE: api/Sellers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeller([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var seller = await _context.Sellers.SingleOrDefaultAsync(m => m.ID == id);
            if (seller == null)
            {
                return NotFound();
            }

            _context.Sellers.Remove(seller);
            await _context.SaveChangesAsync();

            return Ok(seller);
        }

        // POST: api/Sellers/Find
        [Route("Find")]
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

        private bool SellerExists(int id)
        {
            return _context.Sellers.Any(e => e.ID == id);
        }
    }
}