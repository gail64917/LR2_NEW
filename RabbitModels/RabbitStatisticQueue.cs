using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitModels
{
    public class RabbitStatisticQueue
    {
        public int ID { get; set; }
        public string PageName { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Action { get; set; }
        public string Client { get; set; }
        public bool Result { get; set; }
        public string User { get; set; }
    }
}
