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

        [Route("GetMessages")]
        public void GetMessages()
        {
            var Bus = RabbitHutch.CreateBus("host=localhost");
            ConcurrentStack<RabbitStatisticQueue> statisticCollection = new ConcurrentStack<RabbitStatisticQueue>();

            Bus.Receive<RabbitStatistic>("statistic", msg =>
            {
                RabbitStatisticQueue stat = new RabbitStatisticQueue() { ID = msg.ID, Client = msg.Client, Result = msg.Result, Action = msg.Action, PageName = msg.PageName, TimeStamp = msg.TimeStamp, User = msg.User };
                statisticCollection.Push(stat);

            });
            Thread.Sleep(5000);

            foreach (RabbitStatisticQueue a in statisticCollection)
            {
                _context.StatisticFromQueue.Add(a);
                RabbitStatisticQueue rbt = new RabbitStatisticQueue() { PageName = a.PageName, TimeStamp = a.TimeStamp, Action = a.Action, Client = a.Client, Result = a.Result, User = a.User, ID = a.ID };
                var bus = RabbitHutch.CreateBus("host=localhost");
                var message = rbt;
                bus.Send("statisticRecieve", rbt);
            }
            _context.SaveChanges();
        }

        public StatisticsController(StatContext context)
        {
            _context = context;
        }

        // GET: api/Statistics
        [Route("FromQueue")]
        [HttpGet]
        public IEnumerable<RabbitStatisticQueue> GetStatisticFromQueue()
        {
            return _context.StatisticFromQueue;
        }

        // GET: api/Statistics
        [HttpGet]
        public IEnumerable<RabbitStatistic> GetStatistic()
        {
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
            string connectionString = "Server=(localdb)\\mssqllocaldb;Database=Statistic57;Trusted_Connection=True;MultipleActiveResultSets=true";
            string query = string.Format(("INSERT INTO Statistic (Action, Client, PageName, Result, TimeStamp, [User]) " +
                   "VALUES (@Action, @Client, @PageName, @Result, @TimeStamp, @User)"));

            // create connection and command
            using (SqlConnection cn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, cn))
            {
                // define parameters and their values
                //cmd.Parameters.AddWithValue("Action", rs.Action);
                //cmd.Parameters.AddWithValue("Client", rs.Client);
                //cmd.Parameters.AddWithValue("PageName", rs.PageName);
                //cmd.Parameters.AddWithValue("Result", rs.Result);
                //cmd.Parameters.AddWithValue("TimeStamp", rs.TimeStamp);

                cmd.Parameters.Add("Action", SqlDbType.NVarChar).Value = rs.Action;
                cmd.Parameters.Add("Client", SqlDbType.NVarChar).Value = rs.Client;
                cmd.Parameters.Add("PageName", SqlDbType.NVarChar).Value = rs.PageName;
                cmd.Parameters.Add("Result", SqlDbType.Bit).Value = rs.Result;
                cmd.Parameters.Add("TimeStamp", SqlDbType.DateTime2).Value = rs.TimeStamp;
                if (rs.User != null)
                    cmd.Parameters.Add("User", SqlDbType.NVarChar).Value = rs.User;

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