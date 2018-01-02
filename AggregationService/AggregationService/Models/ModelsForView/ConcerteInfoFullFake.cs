using AggregationService.Models.ArenaService;
using AggregationService.Models.ArtistService;
using AggregationService.Models.ConcerteService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Models.ModelsForView
{
    public class ConcerteInfoFullFake
    {
        public int ID { get; set; }
        public string BrandName { get; set; }

        public string ShowName { get; set; }
        public int TicketsNumber { get; set; }
        public int Price { get; set; }
        public DateTime Date { get; set; }

        public string CityName { get; set; }
        public int CityPopulation { get; set; }

        public string ArenaName { get; set; }
        public int ArenaCapacity { get; set; }

        public string ArtistName { get; set; }
        public int LastFmRating { get; set; }

        public List<City> cities { get; set; }
        public List<Seller> brandNames { get; set; }
        public List<Arena> arenas { get; set; }
        public List<Artist> artists { get; set; }
    }
}
