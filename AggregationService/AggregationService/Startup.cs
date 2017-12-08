using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Web.Http;
using AggregationService.Logger;
using System.Web;
using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Subscription;
using RestBus.AspNet;
using RestBus.AspNet.Server;

namespace AggregationService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Arena/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "Default",
                    template: "{controller=Default}/{id=1}");
            });

            //var amqpUrl = "amqp://localhost:5672"; //AMQP URI for RabbitMQ server
            //var serviceName = "Aggregation"; //Uniquely identifies this service

            //var msgMapper = new BasicMessageMapper(amqpUrl, serviceName);
            //var subscriber = new RestBusSubscriber(msgMapper);

            //app.RunRestBusHost(subscriber);
        }
    }
}
