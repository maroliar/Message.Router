using Microsoft.Extensions.Hosting;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Receiving;

namespace Message.Router.MqttClient.Services
{
    public interface IMqttClientService : IHostedService,
                                          IMqttClientConnectedHandler,
                                          IMqttApplicationMessageReceivedHandler
    {
    }
}
