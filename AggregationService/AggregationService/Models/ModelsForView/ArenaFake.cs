using AggregationService.Models.ArenaService;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Models.ModelsForView
{
    public class ArenaFake
    {
        public int ID { get; set; }
        public string ArenaName { get; set; }
        public int Capacity { get; set; }
        public int CityID { get; set; }

        public virtual City City { get; set; }

        public List<City> cities { get; set; }
    }
}
