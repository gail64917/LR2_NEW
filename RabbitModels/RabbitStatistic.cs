using EasyNetQ;
using System;
using System.Collections.Generic;
using System.Text;

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
    }

    public static class StatisticSender
    {
        public static void SendStatistic(string serviceName, DateTime dt, string action, string client, bool result)
        {
            RabbitStatistic rbt = new RabbitStatistic() { PageName = serviceName, TimeStamp = dt, Action = action, Client = client, Result = result };
            var bus = RabbitHutch.CreateBus("host=localhost");
            bus.Send("statistic", rbt);
        }
    }
}
