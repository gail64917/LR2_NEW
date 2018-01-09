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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AggregationService
{
    public partial class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    //.AddCookie()
                    .AddJwtBearer(options => {
                        options.TokenValidationParameters =
                             new TokenValidationParameters
                             {
                                 ValidateIssuer = true,
                                 ValidateAudience = true,
                                 ValidateLifetime = true,
                                 ValidateIssuerSigningKey = true,

                                 ValidIssuer = "Test.Security.Bearer",
                                 ValidAudience = "Test.Security.Bearer",
                                 IssuerSigningKey =
                                 Provider.JWT.JwtSecurityKey.Create("Test-secret-key-1234")
                             };

                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                Console.WriteLine("OnAuthenticationFailed: " + context.Exception.Message);
                                return Task.CompletedTask;
                            },
                            OnTokenValidated = context =>
                            {
                                Console.WriteLine("OnTokenValidated: " + context.SecurityToken);
                                return Task.CompletedTask;
                            }
                        };

                    });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("User",
                    policy => policy.RequireClaim("User"));
                options.AddPolicy("Admin",
                    policy => policy.RequireClaim("Admin"));
                //options.AddPolicy("Hr",
                //    policy => policy.RequireClaim("EmployeeNumber"));
                //options.AddPolicy("Founder",
                //    policy => policy.RequireClaim("EmployeeNumber", "1", "2", "3", "4", "5"));
            });
            
            services.AddMvc();
            services.AddDistributedMemoryCache();
            services.AddSession();
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
            app.UseAuthentication();
            //app.UseMvcWithDefaultRoute();

            app.UseSession();
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
