﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Models.ArtistService
{
    public class Artist
    {
        public int ID { get; set; }
        public string ArtistName { get; set; }
        public int LastFmRating { get; set; }
    }
}
