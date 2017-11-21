using ArenaService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArenaService.Data
{
    public class DbInitializer
    {
        public static void Initialize(ArenaContext context)
        {
            context.Database.EnsureCreated();

            if (context.Cities.Any())
            {
                return;   // DB has been seeded
            }

            var citys = new City[]
            {
                new City{CityName = "Moscow"},
                new City{CityName = "Berlin"},
                new City{CityName = "London"}
            };
            foreach (City c in citys)
            {
                context.Cities.Add(c);
            }
            context.SaveChanges();

            var arenas = new Arena[]
            {
                new Arena{ ArenaName = "Crocus City Hall", CityID = 1},
                new Arena{ ArenaName = "Olimpiyskiy", CityID = 1},
                new Arena{ ArenaName = "Olimpiyskiy", CityID = 1},
                new Arena{ ArenaName = "Wembley Arena", CityID = 3},
                new Arena{ ArenaName = "Brixton Academy", CityID = 3},
                new Arena{ ArenaName = "Mercedes-Benz Arena", CityID = 2},
                new Arena{ ArenaName = "Olympiastadion Berlin", CityID = 2}
            };
            foreach (Arena a in arenas)
            {
                context.Arenas.Add(a);
            }
            context.SaveChanges();
        }
    }
}
