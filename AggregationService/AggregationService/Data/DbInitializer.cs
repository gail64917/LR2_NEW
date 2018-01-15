using RabbitModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Data
{
    public class DbInitializer
    {
        public static void Initialize(StatisticContext context)
        {
            context.Database.EnsureCreated();

            if (context.StatisticEvents.Any())
            {
                return;   // DB has been seeded
            }

            var events = new RabbitStatisticQueue[]
            {
                new RabbitStatisticQueue{ Action = "Index", Client = "localhost", PageName = "Default", Result = true, TimeStamp=DateTime.Now, User="sad" }
            };
            foreach (RabbitStatisticQueue s in events)
            {
                context.StatisticEvents.Add(s);
            }
            context.SaveChanges();
            context.StatisticEvents.Remove(events[0]);
            context.SaveChanges();
        }
    }
}
