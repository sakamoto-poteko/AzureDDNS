using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AzureDDNS.Functions;
using AzureDDNS.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        private OptionsBuilder<T> BindOptionAndValidate<T>(IServiceCollection services, string sectionName) where T : class 
        {
            return services.AddOptions<T>().Configure<IConfiguration>((settings, config) =>
            {
                config.GetSection(sectionName).Bind(settings);
            }).PostConfigure(settings => ValidateWithDataAnnotation(settings));
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        private void ConfigureServices(IServiceCollection services)
        {
            BindOptionAndValidate<Settings.AzureLogin>(services, "AzureLogin");
            BindOptionAndValidate<Settings.DnsZone>(services, "DnsZone");
            BindOptionAndValidate<Settings.Authorization>(services, "Authorization").PostConfigure(settings =>
            {
                if (settings.Enabled)
                {
                    if (string.IsNullOrWhiteSpace(settings.Password))
                    {
                        throw new ValidationException("Password required");
                    }

                    if (string.IsNullOrWhiteSpace(settings.Username))
                    {
                        throw new ValidationException("Username required");
                    }
                }
            });

            services.AddSingleton<IDnsUpdateService, AzureDnsUpdateService>();
            services.AddTransient<UpdateCore>();
        }

    }
}
