namespace Message.Router.MqttClient.Services
{
    public class MqttClientServiceProvider
    {
        public readonly IMqttClientService MqttClientService;

        public MqttClientServiceProvider(IMqttClientService mqttClientService)
        {
            MqttClientService = mqttClientService;
        }
    }
}
