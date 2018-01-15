using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitModels;

namespace StatisticService.Data
{
    public class StatContext : DbContext
    {
        public StatContext(DbContextOptions<StatContext> options) : base(options)
        {
        }

        public DbSet<RabbitStatistic> Statistic { get; set; }
        public DbSet<RabbitStatisticQueue> StatisticFromQueue { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RabbitStatistic>().ToTable("Statistic");
            modelBuilder.Entity<RabbitStatisticQueue>().ToTable("StatisticFromQueue");
        }
    }
}
