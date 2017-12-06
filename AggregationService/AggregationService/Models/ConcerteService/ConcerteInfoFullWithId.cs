using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Models.ConcerteService
{
    public class ConcerteInfoFullWithId
    {
        public int ID { get; set; }
        public string ShowName { get; set; }
        public int TicketsNumber { get; set; }
        public int Price { get; set; }
        public DateTime Date { get; set; }

        public int SellerID { get; set; }
        public string BrandName { get; set; }

        public int CityID { get; set; }
        public string CityName { get; set; }
        public int CityPopulation { get; set; }

        public int ArenaID { get; set; }
        public string ArenaName { get; set; }
        public int ArenaCapacity { get; set; }

        public int ArtistID { get; set; }
        public string ArtistName { get; set; }
        public int LastFmRating { get; set; }
    }
}
