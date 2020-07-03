using Message.Router.MqttClient.Extensions;
using Message.Router.MqttClient.Services;
using Message.Router.MqttClient.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Message.Router.MqttClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            MapConfiguration();
        }

        public IConfiguration Configuration;

        private void MapConfiguration()
        {
            MapBrokerHostSettings();
            MapClientSettings();
            MapBrokerTopics();
        }

        private void MapBrokerHostSettings()
        {
            BrokerHostSettings brokerHostSettings = new BrokerHostSettings();
            Configuration.GetSection(nameof(BrokerHostSettings)).Bind(brokerHostSettings);
            AppSettingsProvider.BrokerHostSettings = brokerHostSettings;
        }

        private void MapClientSettings()
        {
            ClientSettings clientSettings = new ClientSettings();
            Configuration.GetSection(nameof(ClientSettings)).Bind(clientSettings);
            AppSettingsProvider.ClientSettings = clientSettings;
        }

        private void MapBrokerTopics()
        {
            BrokerTopics brokerTopics = new BrokerTopics();
            Configuration.GetSection(nameof(BrokerTopics)).Bind(brokerTopics);
            AppSettingsProvider.BrokerTopics = brokerTopics;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMqttClientHostedService();
            services.AddSingleton<ExternalService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.();
            //});
        }
    }
}
