using AggregationService.Provider;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Security.OAuth;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;
using DataProtectionProviderDelegate = System.Func<string[], System.Tuple<System.Func<byte[], byte[]>, System.Func<byte[], byte[]>>>;
using DataProtectionTuple = System.Tuple<System.Func<byte[], byte[]>, System.Func<byte[], byte[]>>;

namespace AggregationService
{
    public partial class Startup
    {
        public static OAuthAuthorizationServerOptions OAuthOptions { get; private set; }

        static Startup()
        {
            OAuthOptions = new OAuthAuthorizationServerOptions
            {
                TokenEndpointPath = new Microsoft.Owin.PathString("/token"),
                Provider = new OAuthAppProvider(),
                AccessTokenExpireTimeSpan = TimeSpan.FromMinutes(30),
                AllowInsecureHttp = true
            };
        }

        private void ConfigureAuth(IAppBuilder app)
        {
            app.UseOAuthBearerTokens(OAuthOptions);
        }
    }


    public static class KatanaIApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseAppBuilder(this IApplicationBuilder app, Action<IAppBuilder> configure)
        {
            app.UseOwin(addToPipeline =>
            {
                addToPipeline(next =>
                {
                    var appBuilder = new AppBuilder();
                    appBuilder.Properties["builder.DefaultApp"] = next;
                    appBuilder.Properties["host.AppName"] = "app";
                    configure(appBuilder);
                    
                    return appBuilder.Build<AppFunc>();
                });
            });
            return app;
        }

        public static IAppBuilder SetDataProtectionProvider(this IAppBuilder appBuilder, IApplicationBuilder app)
        {
            var provider = app.ApplicationServices.GetRequiredService<IDataProtectionProvider>();
            appBuilder.Properties["security.DataProtectionProvider"] = new DataProtectionProviderDelegate(purposes =>
            {
                var dataProtection = provider.CreateProtector(string.Join(",", purposes));
                return new DataProtectionTuple(dataProtection.Protect, dataProtection.Unprotect);
            });
            return appBuilder;
        }
    }
}
