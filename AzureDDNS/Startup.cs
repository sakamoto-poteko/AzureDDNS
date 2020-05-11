using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AzureDDNS.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(AzureDDNS.Startup))]


namespace AzureDDNS
{

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            ConfigureServices(builder.Services);
        }

        private void ValidateWithDataAnnotation<T>(T settings)
        {
            var context = new ValidationContext(settings, null, null);
            Validator.ValidateObject(settings, context);
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions<Settings.AzureLogin>().Configure<IConfiguration>((settings, config) =>
            {
                config.GetSection("AzureLogin").Bind(settings);
            }).PostConfigure(settings => ValidateWithDataAnnotation(settings));

            services.AddOptions<Settings.DnsZone>().Configure<IConfiguration>((settings, config) =>
            {
                config.GetSection("DnsZone").Bind(settings);
            }).PostConfigure(settings => ValidateWithDataAnnotation(settings));
            
            services.AddSingleton<IDnsUpdateService, AzureDnsUpdateService>();
        }

    }
}
