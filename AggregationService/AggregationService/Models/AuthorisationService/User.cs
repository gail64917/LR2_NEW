using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Models.AuthorisationService
{
    public class User
    {
        public int ID { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
    }
}
