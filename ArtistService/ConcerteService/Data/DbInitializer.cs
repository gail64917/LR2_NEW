using ConcerteService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConcerteService.Data
{
    public class DbInitializer
    {
        public static void Initialize(ArtistContext context)
        {
            context.Database.EnsureCreated();

            if (context.Artists.Any())
            {
                return;   // DB has been seeded
            }

            var artists = new Artist[]
            {
                new Artist { ArtistName = "Marilyn Manson", LastFmRating = 10},
                new Artist { ArtistName = "30 Seconds to Mars", LastFmRating = 7},
                new Artist { ArtistName = "Rammstein", LastFmRating = 4},
                new Artist { ArtistName = "The Beatles", LastFmRating = 1},
                new Artist { ArtistName = "Suicide Silence", LastFmRating = 14},
                new Artist { ArtistName = "Depeche mode", LastFmRating = 2},
                new Artist { ArtistName = "Bullet for my valentine", LastFmRating = 15},
                new Artist { ArtistName = "Frank Sinatra", LastFmRating = 3},
                new Artist { ArtistName = "Elvis Presley", LastFmRating = 4},
                new Artist { ArtistName = "Combichrist", LastFmRating = 8},
                new Artist { ArtistName = "Devil sold his soul", LastFmRating = 9}
            };

            foreach (Artist s in artists)
            {
                context.Artists.Add(s);
            }
            context.SaveChanges();
        }
    }
}
