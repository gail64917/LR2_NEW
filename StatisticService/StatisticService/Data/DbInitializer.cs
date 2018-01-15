using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StatisticService.Data
{
    public class DbInitializer
    {
        public static void Initialize(StatContext context)
        {
            context.Database.EnsureCreated();

            if (context.Statistic.Any())
            {
                return;   // DB has been seeded
            }

            var statistic = new RabbitModels.RabbitStatistic[]
            {
                new RabbitModels.RabbitStatistic() { Action = "Index", Client = "localhost", Result = true, PageName = "AggregationService", TimeStamp = DateTime.Now, User = "sad" }
            };
            foreach (RabbitModels.RabbitStatistic s in statistic)
            {
                context.Statistic.Add(s);
            }
            context.SaveChanges();

            var statisticQueue = new RabbitModels.RabbitStatisticQueue[]
            {
                new RabbitModels.RabbitStatisticQueue() { Action = "Index", Client = "localhost", Result = true, PageName = "AggregationService", TimeStamp = DateTime.Now, User = "sad" }
            };
            foreach (RabbitModels.RabbitStatisticQueue s in statisticQueue)
            {
                context.StatisticFromQueue.Add(s);
            }

            context.SaveChanges();
        }
    }
}
