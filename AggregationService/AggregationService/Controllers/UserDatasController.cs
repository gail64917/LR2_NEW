using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AggregationService.Data;
using AggregationService.Models.AuthorisationService;

namespace AggregationService.Controllers
{
    [Produces("application/json")]
    [Route("api/UserDatas")]
    public class UserDatasController : Controller
    {
        private readonly StatisticContext _context;

        public UserDatasController(StatisticContext context)
        {
            _context = context;
        }

        // GET: api/UserDatas
        [HttpGet]
        public IEnumerable<UserData> GetUserData()
        {
            return _context.UserData;
        }

        // GET: api/UserDatas/getbycode
        [Route("GetByCode")]
        [HttpGet("{code}")]
        public UserData GetUserDataByCode([FromQuery] string code)
        {
            UserData result = new UserData();
            foreach (UserData item in _context.UserData)
            {
                if (item.Code == code)
                    result = item;
            }
            return result;
        }

        // GET: api/UserDatas/getByLogin
        [Route("GetByLogin")]
        [HttpGet("{login}")]
        public UserData GetUserDataByLogin([FromQuery] string login)
        {
            UserData result = new UserData();
            foreach (UserData item in _context.UserData)
            {
                if (item.Login == login)
                    result = item;
            }
            return result;
        }

        //// GET: api/UserDatas/5
        //[HttpGet("{id}")]
        //public async Task<IActionResult> GetUserData([FromRoute] int id)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    var userData = await _context.UserData.SingleOrDefaultAsync(m => m.ID == id);

        //    if (userData == null)
        //    {
        //        return NotFound();
        //    }

        //    return Ok(userData);
        //}

        // PUT: api/UserDatas/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUserData([FromRoute] int id, [FromBody] UserData userData)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != userData.ID)
            {
                return BadRequest();
            }

            _context.Entry(userData).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserDataExists(id))
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

        // POST: api/UserDatas
        [HttpPost]
        public async Task<IActionResult> PostUserData([FromBody] UserData userData)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.UserData.Add(userData);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUserData", new { id = userData.ID }, userData);
        }

        // DELETE: api/UserDatas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserData([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userData = await _context.UserData.SingleOrDefaultAsync(m => m.ID == id);
            if (userData == null)
            {
                return NotFound();
            }

            _context.UserData.Remove(userData);
            await _context.SaveChangesAsync();

            return Ok(userData);
        }

        private bool UserDataExists(int id)
        {
            return _context.UserData.Any(e => e.ID == id);
        }
    }
}