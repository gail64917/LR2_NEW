using AggregationService.Models.AuthorisationService;
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

            if (!context.UserData.Any())
            {
                //for (int i = 0; i<1004; i++)
                //{
                //    UserData ud = new UserData() { Login = "fake" };

                //    context.UserData.Add(ud);
                //}
                //context.SaveChanges();
            }

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
