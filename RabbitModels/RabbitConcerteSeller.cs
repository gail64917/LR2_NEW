using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitModels
{
    public class RabbitConcerteSeller
    {
        public string BrandName { get; set; }

        public string ShowName { get; set; }
        public int TicketsNumber { get; set; }
        public int Price { get; set; }
        public string CityName { get; set; }
        public string ArenaName { get; set; }
        public string ArtistName { get; set; }
        public DateTime Date { get; set; }
    }
}
