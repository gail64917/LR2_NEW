using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RabbitModels;
using StatisticService.Data;
using EasyNetQ;
using System.Collections.Concurrent;
using System.Threading;
using System.Data.SqlClient;
using System.Data;

namespace StatisticService.Controllers
{
    [Produces("application/json")]
    [Route("api/Statistics")]
    public class StatisticsController : Controller
    {
        private readonly StatContext _context;

        public StatisticsController(StatContext context)
        {
            _context = context;
        }

        // GET: api/Statistics
        [HttpGet]
        public IEnumerable<RabbitStatistic> GetStatistic()
        {
            var Bus = RabbitHutch.CreateBus("host=localhost");
            ConcurrentStack<RabbitStatistic> statisticCollection = new ConcurrentStack<RabbitStatistic>();

            Bus.Receive<RabbitStatistic>("statistic", msg =>
            {
                RabbitStatistic stat = new RabbitStatistic() { Client = msg.Client, Result = msg.Result, Action = msg.Action, PageName = msg.PageName, TimeStamp = msg.TimeStamp };
                statisticCollection.Push(stat);
            });
            Thread.Sleep(5000);

            foreach (RabbitStatistic a in statisticCollection)
            {
                _context.Statistic.Add(a);
            }
            _context.SaveChanges();
            return _context.Statistic;
        }

        // GET: api/Statistics/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRabbitStatistic([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rabbitStatistic = await _context.Statistic.SingleOrDefaultAsync(m => m.ID == id);

            if (rabbitStatistic == null)
            {
                return NotFound();
            }

            return Ok(rabbitStatistic);
        }

        public static void DbPush(RabbitStatistic rs)
        {
            //_context.Statistic.Add(rs);
            //_context.SaveChanges();
            string connectionString = "Server=(localdb)\\mssqllocaldb;Database=Statistic52;Trusted_Connection=True;MultipleActiveResultSets=true";
            string query = "INSERT INTO dbo.Statistic (Action, Client, PageName, Result, TimeStamp) " +
                   "VALUES (@Action, @Client, @PageName, @Result, @TimeStamp) ";

            // create connection and command
            using (SqlConnection cn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, cn))
            {
                // define parameters and their values
                cmd.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = rs.Action;
                cmd.Parameters.Add("@Client", SqlDbType.NVarChar, 50).Value = rs.Client;
                cmd.Parameters.Add("@PageName", SqlDbType.NVarChar, 50).Value = rs.PageName;
                cmd.Parameters.Add("@Result", SqlDbType.Bit).Value = rs.Result;
                cmd.Parameters.Add("@PageName", SqlDbType.DateTime2, 7).Value = rs.TimeStamp;

                // open connection, execute INSERT, close connection
                cn.Open();
                cmd.ExecuteNonQuery();
                cn.Close();
            }
        }

        // PUT: api/Statistics/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRabbitStatistic([FromRoute] int id, [FromBody] RabbitStatistic rabbitStatistic)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != rabbitStatistic.ID)
            {
                return BadRequest();
            }

            _context.Entry(rabbitStatistic).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RabbitStatisticExists(id))
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

        // POST: api/Statistics
        [HttpPost]
        public async Task<IActionResult> PostRabbitStatistic([FromBody] RabbitStatistic rabbitStatistic)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Statistic.Add(rabbitStatistic);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetRabbitStatistic", new { id = rabbitStatistic.ID }, rabbitStatistic);
        }

        // DELETE: api/Statistics/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRabbitStatistic([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var rabbitStatistic = await _context.Statistic.SingleOrDefaultAsync(m => m.ID == id);
            if (rabbitStatistic == null)
            {
                return NotFound();
            }

            _context.Statistic.Remove(rabbitStatistic);
            await _context.SaveChangesAsync();

            return Ok(rabbitStatistic);
        }

        private bool RabbitStatisticExists(int id)
        {
            return _context.Statistic.Any(e => e.ID == id);
        }
    }
}