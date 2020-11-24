namespace Message.Router.MqttClient.Settings
{
    public class BrokerTopics
    {
        public string TopicoMessageRouter { get; set; }
        
        public string TopicoGatewaySMS { get; set; }
        public string TopicoGatewaySMSEntrada { get; set; }
        public string TopicoGatewaySMSSaida { get; set; }

        public string TopicoGatewayTelegram { get; set; }
        public string TopicoGatewayTelegramEntrada { get; set; }
        public string TopicoGatewayTelegramSaida { get; set; }
        
        public string TopicoTemperatura { get; set; }
        public string TopicoDesodorizacao { get; set; }
        public string TopicoInterfone { get; set; }
        public string TopicoPets { get; set; }

        public string TopicoConfig { get; set; }

    }
}
