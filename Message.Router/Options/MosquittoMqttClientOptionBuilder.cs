using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Message.Router.Options
{
    public class MosquittoMqttClientOptionBuilder : MqttClientOptionsBuilder
    {
        public IServiceProvider ServiceProvider { get; }

        public MosquittoMqttClientOptionBuilder(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
}
