using Message.Router.Entities;
using Message.Router.Settings;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace Message.Router.Client
{
    public class MosquittoMqttClient : IMosquittoMqttClient
    {
        private readonly BrokerTopics brokerTopics = AppSettingsProvider.BrokerTopics;
        private readonly ClientSettings clientSettings = AppSettingsProvider.ClientSettings;

        private readonly IMqttClientOptions Options;
        private readonly IMqttClient mqttClient;

        private readonly string menu = "Home Automation"
            + "\r\nEscolha a opcao abaixo: "
            + "\r\nOP1 - Informar Temperatura "
            + "\r\nOP2 - Desodorizar Ambiente "
            + "\r\nOP3 - Abrir Portaria "
            + "\r\nOP4 - Alimentar Pets";

        public MosquittoMqttClient(IMqttClientOptions options)
        {
            Options = options;
            mqttClient = new MqttFactory().CreateMqttClient();
            mqttClient.UseApplicationMessageReceivedHandler(OnMqttMessageAsync);
        }

        public async Task OnMqttMessageAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            try
            {
                var jsonPayload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
                var topic = eventArgs.ApplicationMessage.Topic;

                Console.WriteLine("Nova mensagem recebida do broker, no topico de: " + topic);
                Console.WriteLine(jsonPayload);

                if (topic.Contains(brokerTopics.TopicoGatewaySMSEntrada))
                {
                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);
                    await HandleSMSTopicAsync(payload);
                }
                if (topic.Contains(brokerTopics.TopicoGatewayTelegramEntrada))
                {
                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);
                    await HandleTelegramTopicAsync(payload);
                }


                // Resposta dos devices:
                
                if (topic.Contains(brokerTopics.TopicoTemperatura))
                {
                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);
                    await HandleTemperaturaTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoDesodorizacao))
                {
                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);
                    await HandleDesodorizacaoTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoInterfone))
                {
                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);
                    await HandleInterfoneTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoPets))
                {
                    var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);
                    await HandlePetsTopicAsync(payload);
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao tentar ler a mensagem: " + ex.Message);
                //throw;
            }
        }

        public async Task HandleSMSTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                switch (payload.message.ToUpper())
                {
                    case "MENU":

                        payload.message = menu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);

                        break;

                    case "OP1": // Checar Temperatura

                        Random rnd = new Random();
                        int temp = rnd.Next(10, 40);

                        payload.message = "Temperatura no momento: " + temp + " graus.";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);

                        break;

                    case "OP2": // Desodorizar Ambiente

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "OP3": // Abrir Portaria

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "OP4": // Alimentar Pets

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;

                    default:

                        payload.message = "Opcao Invalida!";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);

                        break;
                }
            }
        }

        public async Task HandleTelegramTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                switch (payload.message.ToUpper())
                {
                    case "MENU":

                        payload.message = menu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

                        break;

                    case "OP1":
                    case "INFORMAR TEMPERATURA":

                        Random rnd = new Random();
                        int temp = rnd.Next(10, 40);

                        payload.message = "Temperatura no momento: " + temp + " graus.";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

                        break;

                    case "OP2":
                    case "DESODORIZAR AMBIENTE":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "OP3":
                    case "ABRIR PORTARIA":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "OP4":
                    case "ALIMENTAR PETS":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;

                    default:

                        payload.message = "Opcao Invalida!";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

                        break;
                }
            }
        }



        // RESPOSTA DOS DEVICES
        
        public async Task HandleTemperaturaTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                // Por enquanto, apenas envia notificações pelo Telegram, não por SMS
                if (int.TryParse(payload.message, out _) && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "Temperatura do Raspberry muito alta! " + payload.message;
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }
            }
        }

        public async Task HandleDesodorizacaoTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "Desodorizacao Executada!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "Desodorizacao Executada!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }
            }
        }

        public async Task HandleInterfoneTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "Portaria Aberta!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "Portaria Aberta!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }

                // Por enquanto, apenas envia notificações pelo Telegram, não por SMS
                if (payload.message.ToUpper() == "RING" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "Parece que tem alguem tocando o Interfone!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }
            }
        }

        public async Task HandlePetsTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "Pets Alimentados!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "Pets Alimentados!";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }
            }
        }






        public string PrepareMsgToBroker(Payload payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                //WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return jsonPayload;
        }

        public async Task PublishMqttClientAsync(string topic, string payload, bool retainFlag = false, int qos = 0)
        {
            Console.WriteLine("Enviando mensagem para o broker: ");
            Console.WriteLine(payload);

            Console.Write("Topico: ");
            Console.WriteLine(topic);

            await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .WithRetainFlag(retainFlag)
                .Build());
        }

        public async Task StartMqttClientAsync()
        {
            await mqttClient.ConnectAsync(Options);

            // anuncia status online
            Payload payload = new Payload
            {
                device = clientSettings.Id,
                source = "Internal",
                message = "Online"
            };

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoMessageRouter).Build());

            var serializedDeviceStatus = PrepareMsgToBroker(payload);
            await PublishMqttClientAsync(brokerTopics.TopicoMessageRouter, serializedDeviceStatus);

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewaySMSEntrada).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewaySMSSaida).Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramEntrada).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramSaida).Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoTemperatura).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoDesodorizacao).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoInterfone).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoPets).Build());

            if (!mqttClient.IsConnected)
            {
                await mqttClient.ReconnectAsync();
            }
        }

        public Task StopMqttClientAsync()
        {
            throw new NotImplementedException();
        }
    }
}
