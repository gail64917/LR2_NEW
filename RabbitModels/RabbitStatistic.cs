using EasyNetQ;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using System.Data.SqlClient;
using System.Data;

namespace RabbitModels
{
    public class RabbitStatistic
    {
        public int ID { get; set; }
        public string PageName { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Action { get; set; }
        public string Client { get; set; }
        public bool Result { get; set; }
        public string User { get; set; }
    }

    public static class StatisticSender
    {
        public static void SendStatistic(string serviceName, DateTime dt, string action, string client, bool result, string user)
        {
            RabbitStatistic rbt = new RabbitStatistic() { PageName = serviceName, TimeStamp = dt, Action = action, Client = client, Result = result, User = user };
            var bus = RabbitHutch.CreateBus("host=localhost");
            var message = rbt;
            bus.Publish(message);

            RabbitStatisticQueue rbtQueue = new RabbitStatisticQueue() { PageName = serviceName, TimeStamp = dt, Action = action, Client = client, Result = result, User = user };

            //пишем в бд событие
            string connection = "Server=(localdb)\\mssqllocaldb;Database=StatisticEvents53;Trusted_Connection=True;MultipleActiveResultSets=true";
            EventDbSender(rbtQueue, connection);

            //находим его ID
            rbtQueue.ID = EventDbFinder(rbtQueue, connection);

            //отправляем его в очередь
            bus.Send("statistic", rbtQueue);
        }

        private static void EventDbSender(RabbitStatisticQueue rs, string connectionStringDb)
        {
            //_context.Statistic.Add(rs);
            //_context.SaveChanges();
            string connectionString = connectionStringDb;
            string query = string.Format("INSERT INTO StatisticEvents (Action, Client, PageName, Result, TimeStamp, [User]) " +
                    "VALUES (@Action, @Client, @PageName, @Result, @TimeStamp, @User)");

            // create connection and command
            using (SqlConnection cn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, cn))
            {
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

        private static int EventDbFinder(RabbitStatisticQueue rs, string connectionStringDb)
        {
            int ID = 0;
            string connectionString = connectionStringDb;
            //string query = string.Format("SELECT * FROM StatisticEvents WHERE Action = @Action AND Client = @Client AND PageName = @PageName AND Result = @Result AND TimeStamp = @TimeStamp AND User = @User");
            string query = string.Format("SELECT * FROM StatisticEvents WHERE TimeStamp = @TimeStamp");

            // create connection and command
            using (SqlConnection cn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, cn))
            {
                //cmd.Parameters.Add("Action", SqlDbType.NVarChar).Value = rs.Action;
                //cmd.Parameters.Add("Client", SqlDbType.NVarChar).Value = rs.Client;
                //cmd.Parameters.Add("PageName", SqlDbType.NVarChar).Value = rs.PageName;
                //cmd.Parameters.Add("Result", SqlDbType.Bit).Value = rs.Result;
                cmd.Parameters.Add("TimeStamp", SqlDbType.DateTime2).Value = rs.TimeStamp;
                //if (rs.User != null)
                //    cmd.Parameters.Add("User", SqlDbType.NVarChar).Value = rs.User;

                // open connection, execute INSERT, close connection
                cn.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ID = Convert.ToInt32(reader[0]);
                }
                //cmd.ExecuteNonQuery();
                cn.Close();
            }
            return ID;
        }
    }
}
