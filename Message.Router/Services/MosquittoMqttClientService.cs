using Message.Router.Client;
using Microsoft.Extensions.Hosting;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Message.Router.Services
{
    public class MosquittoMqttClientService : IHostedService
    {
        private readonly MosquittoMqttClient client;

        public MosquittoMqttClientService(IMqttClientOptions options)
        {
            client = new MosquittoMqttClient(options);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return client.StartMqttClientAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return client.StopMqttClientAsync();
        }
    }
}
