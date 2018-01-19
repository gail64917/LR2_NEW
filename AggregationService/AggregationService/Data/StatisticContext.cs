using AggregationService.Models.AuthorisationService;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregationService.Data
{
    public class StatisticContext : DbContext
    {
        public StatisticContext(DbContextOptions<StatisticContext> options) : base(options)
        {
        }

        public DbSet<RabbitModels.RabbitStatisticQueue> StatisticEvents { get; set; }
        public DbSet<UserData> UserData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RabbitModels.RabbitStatisticQueue>().ToTable("StatisticEvents");
            modelBuilder.Entity<UserData>().ToTable("UserData");
        }
    }
}
