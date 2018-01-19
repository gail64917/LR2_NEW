using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Models.AuthorisationService
{
    public class UserData
    {
        public int ID { get; set; }
        public string Login { get; set; }
        public string Code { get; set; }
        public string Role { get; set; }
        public string LastToken { get; set; }
        public string clientID { get; set; }
        public string clientSecret { get; set; }
    }
}
