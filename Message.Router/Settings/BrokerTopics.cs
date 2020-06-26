﻿namespace Message.Router.Settings
{
    public class BrokerTopics
    {
        public string TopicoMessageRouter { get; set; }

        public string TopicoGatewaySMSEntrada { get; set; }
        public string TopicoGatewaySMSSaida { get; set; }
        
        public string TopicoGatewayTelegramEntrada { get; set; }
        public string TopicoGatewayTelegramSaida { get; set; }
        
        public string TopicoTemperatura { get; set; }
        public string TopicoDesodorizacao { get; set; }
        public string TopicoInterfone { get; set; }
        public string TopicoPets { get; set; }
    }
}
