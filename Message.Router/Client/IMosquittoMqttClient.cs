using System.Threading.Tasks;

namespace Message.Router.Client
{
    public interface IMosquittoMqttClient
    {
        Task StartMqttClientAsync();
        Task StopMqttClientAsync();
    }
}
