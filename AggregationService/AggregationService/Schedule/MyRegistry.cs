using EasyNetQ;
using FluentScheduler;
using RabbitModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static AggregationService.Logger.RabbitLogger;

namespace AggregationService.Schedule
{
    public class MyRegistryAgg : Registry
    {
        public MyRegistryAgg()
        {
            Thread newThread = new Thread(Work);
            newThread.Start();
        }

        private void Work()
        {
            Schedule(() => ScheduleAction()).ToRunNow().AndEvery(3).Minutes();
        }

        private async void ScheduleAction()
        {
            var Bus = RabbitHutch.CreateBus("host=localhost");
            ConcurrentStack<RabbitStatisticQueue> statisticCollection = new ConcurrentStack<RabbitStatisticQueue>();

            Bus.Receive<RabbitStatisticQueue>("statisticRecieve", msg =>
            {
                RabbitStatisticQueue stat = new RabbitStatisticQueue() { ID = msg.ID, Client = msg.Client, Result = msg.Result, Action = msg.Action, PageName = msg.PageName, TimeStamp = msg.TimeStamp, User = msg.User };
                statisticCollection.Push(stat);

            });
            Thread.Sleep(15000);

            string connectionString = "Server=(localdb)\\mssqllocaldb;Database=StatisticEvents52;Trusted_Connection=True;MultipleActiveResultSets=true";

            //удаляем все сообщения, полученные из Раббита, из БД
            foreach (RabbitStatisticQueue a in statisticCollection)
            {
                try
                {
                    EventDbDeletor(a, connectionString);
                }
                catch
                {
                    await LogMessage("Unexpected message");
                }
            }

            //
            //Осматриваем БД и кидаем заново не удаленные
            //
            //Достаем все из БД
            List<RabbitStatisticQueue> list = new List<RabbitStatisticQueue>();
            string queryStr = "SELECT * FROM StatisticEvents";
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(queryStr, connection))
            {
                //SqlCommand command = new SqlCommand();
                //command.Connection = connection;
                //command.CommandType = System.Data.CommandType.Text;
                try
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string qry = reader[0].ToString() + reader[1].ToString() + reader[2].ToString() + reader[3].ToString() + reader[4].ToString() + reader[5].ToString() + reader[6].ToString();
                        RabbitStatisticQueue rsq = new RabbitStatisticQueue()
                        {
                            ID = Convert.ToInt32(reader[0]),
                            Action = Convert.ToString(reader[1]),
                            Client = Convert.ToString(reader[2]),
                            PageName = Convert.ToString(reader[3]),
                            Result = reader.GetBoolean(reader.GetOrdinal("Result")),
                            TimeStamp = Convert.ToDateTime(reader[5]),
                            User = Convert.ToString(reader[6]),
                        };
                        list.Add(rsq);
                    }
                    reader.Close();
                }
                catch
                {
                    //Console.WriteLine(ex.Message);
                }

            }

            //Если прошло больше 60 минут безуспешных попыток - пишем в лог. Иначе - пытаемся кинуть заново
            foreach (RabbitStatisticQueue item in list)
            {
                //if (item.TimeStamp.AddMinutes(60) < DateTime.Now && item.TimeStamp.AddMinutes(90) > DateTime.Now)
                //    await LogMessage("Cannot Send: " + item.ID + item.Action + item.Client + item.PageName + item.Result + item.TimeStamp + item.User);
                //else if (item.TimeStamp.AddMinutes(60) > DateTime.Now && item.TimeStamp < DateTime.Now)
                //    Bus.Send("statistic", item);
                if (item.TimeStamp.AddMinutes(60) < DateTime.Now)
                {
                    await LogMessage("Cannot Send: " + item.ID + item.Action + item.Client + item.PageName + item.Result + item.TimeStamp + item.User);
                    EventDbDeletor(item, connectionString);
                }
                else
                    Bus.Send("statistic", item);
            }

            Bus.Dispose();
        }

        //private static void EventDbSender(RabbitStatisticQueue rs, string connectionStringDb)
        //{
        //    //_context.Statistic.Add(rs);
        //    //_context.SaveChanges();
        //    string connectionString = connectionStringDb;
        //    string query = string.Format("INSERT INTO StatisticFromQueue (Action, Client, PageName, Result, TimeStamp, [User]) " +
        //            "VALUES (@Action, @Client, @PageName, @Result, @TimeStamp, @User)");

        //    // create connection and command
        //    using (SqlConnection cn = new SqlConnection(connectionString))
        //    using (SqlCommand cmd = new SqlCommand(query, cn))
        //    {
        //        cmd.Parameters.Add("Action", SqlDbType.NVarChar).Value = rs.Action;
        //        cmd.Parameters.Add("Client", SqlDbType.NVarChar).Value = rs.Client;
        //        cmd.Parameters.Add("PageName", SqlDbType.NVarChar).Value = rs.PageName;
        //        cmd.Parameters.Add("Result", SqlDbType.Bit).Value = rs.Result;
        //        cmd.Parameters.Add("TimeStamp", SqlDbType.DateTime2).Value = rs.TimeStamp;
        //        if (rs.User != null)
        //            cmd.Parameters.Add("User", SqlDbType.NVarChar).Value = rs.User;

        //        // open connection, execute INSERT, close connection
        //        cn.Open();
        //        cmd.ExecuteNonQuery();
        //        cn.Close();
        //    }
        //}

        private void EventDbDeletor(RabbitStatisticQueue rs, string connectionStringDb)
        {
            try
            {
                string connectionString = connectionStringDb;
                string query = string.Format("DELETE FROM StatisticEvents WHERE (ID = @ID AND TimeStamp = @TimeStamp)");
                // create connection and command
                using (SqlConnection cn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand(query, cn))
                {
                    cmd.Parameters.Add("TimeStamp", SqlDbType.DateTime2).Value = rs.TimeStamp;
                    cmd.Parameters.Add("ID", SqlDbType.Int).Value = rs.ID;

                    // open connection, execute INSERT, close connection
                    cn.Open();
                    cmd.ExecuteNonQuery();
                    cn.Close();
                }
            }
            catch
            {
                //await LogMessage("Problems with deleting from table");
            }
        }
    }
}
