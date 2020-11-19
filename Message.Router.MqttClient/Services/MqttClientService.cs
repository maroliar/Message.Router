using Message.Router.MqttClient.Entities;
using Message.Router.MqttClient.Settings;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Message.Router.MqttClient.Services
{
    public class MqttClientService : IMqttClientService
    {
        private readonly IMqttClientOptions _options;
        private readonly ILogger<MqttClientService> _logger;

        private readonly IMqttClient mqttClient;

        private readonly BrokerTopics brokerTopics = AppSettingsProvider.BrokerTopics;
        private readonly ClientSettings clientSettings = AppSettingsProvider.ClientSettings;

        private readonly string menu = "Home Automation"
            + "\r\nDigite: "
            + "\r\n1 para Informar Temperatura "
            + "\r\n2 para Desodorizar Ambiente "
            + "\r\n3 para Abrir Portaria "
            + "\r\n4 para Alimentar Pets";

        public MqttClientService(IMqttClientOptions options, ILogger<MqttClientService> logger)
        {
            _options = options;
            _logger = logger;
            mqttClient = new MqttFactory().CreateMqttClient();
            ConfigureMqttClient();
        }

        private void ConfigureMqttClient()
        {
            mqttClient.ConnectedHandler = this;
            //mqttClient.DisconnectedHandler = this;
            mqttClient.ApplicationMessageReceivedHandler = this;
        }

        public async Task HandleConnectedAsync(MqttClientConnectedEventArgs eventArgs)
        {
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoMessageRouter).Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewaySMSEntrada).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewaySMSSaida).Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramEntrada).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoGatewayTelegramSaida).Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoTemperatura).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoDesodorizacao).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoInterfone).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoPets).Build());
        }

        //public Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
        //{
        //    // ação de gravar no log a desconeccao
        //    throw new NotImplementedException();
        //}

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await mqttClient.ConnectAsync(_options);

            // anuncia status online
            Payload payload = new Payload
            {
                device = clientSettings.Id,
                source = "Internal",
                message = "Online"
            };

            var serializedDeviceStatus = PrepareMsgToBroker(payload);
            await PublishMqttClientAsync(brokerTopics.TopicoMessageRouter, serializedDeviceStatus);

            _logger.LogInformation(serializedDeviceStatus);

            if (!mqttClient.IsConnected)
            {
                await mqttClient.ReconnectAsync();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // o app ser encerrado

            if (cancellationToken.IsCancellationRequested)
            {
                var disconnectOption = new MqttClientDisconnectOptions
                {
                    ReasonCode = MqttClientDisconnectReason.NormalDisconnection,
                    ReasonString = "NormalDiconnection"
                };
                await mqttClient.DisconnectAsync(disconnectOption, cancellationToken);
            }
            await mqttClient.DisconnectAsync();

            _logger.LogInformation("Encerrando...");

        }


        public async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            try
            {
                var jsonPayload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
                var payload = JsonSerializer.Deserialize<Payload>(jsonPayload);

                var topic = eventArgs.ApplicationMessage.Topic;

                _logger.LogInformation(string.Format("Nova mensagem recebida do broker, no topico: {0}", topic));
                _logger.LogInformation(jsonPayload);

                if (topic.Contains(brokerTopics.TopicoGatewaySMSEntrada))
                {
                    await HandleSMSTopicAsync(payload);
                }
                if (topic.Contains(brokerTopics.TopicoGatewayTelegramEntrada))
                {
                    await HandleTelegramTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoTemperatura))
                {
                    await HandleTemperaturaTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoDesodorizacao))
                {
                    await HandleDesodorizacaoTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoInterfone))
                {
                    await HandleInterfoneTopicAsync(payload);
                }

                if (topic.Contains(brokerTopics.TopicoPets))
                {
                    await HandlePetsTopicAsync(payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar ler a mensagem: " + ex.Message);
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

                    case "1": 

                        Random rnd = new Random();
                        int temp = rnd.Next(10, 40);

                        payload.message = "Temperatura no momento: " + temp + " graus.";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);

                        break;

                    case "2": 

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "3":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "4":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;


                    // OPÇÕES ADMINISTRATIVAS, OCULTAS POR DEFAULT AO MENU, E SEM RESPOSTA POR ENQUANTO

                    case "RST PETS": // RESTART PETS

                        payload.message = "RST"; 
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;

                    case "RST INT": // RESTART INTERFONE

                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "RST BRK": // RESTART BROKER

                        payload.message = "RESTART";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoConfig, serializedPayload);

                        break;

                    case "STD BRK": // SHUTDOWN BROKER

                        payload.message = "SHUTDOWN";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoConfig, serializedPayload);

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

                    case "1":

                        Random rnd = new Random();
                        int temp = rnd.Next(10, 40);

                        payload.message = "Temperatura no momento: " + temp + " graus.";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

                        break;

                    case "2":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "3":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "4":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;


                    // OPÇÕES ADMINISTRATIVAS, OCULTAS POR DEFAULT AO MENU, E SEM RESPOSTA POR ENQUANTO

                    case "RST PETS": // RESTART PETS

                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;

                    case "RST INT": // RESTART INTERFONE

                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "RST BRK": // RESTART BROKER

                        payload.message = "RESTART";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoConfig, serializedPayload);

                        break;

                    case "STD BRK": // SHUTDOWN BROKER

                        payload.message = "SHUTDOWN";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoConfig, serializedPayload);

                        break;


                    default:

                        payload.message = "Opcao Invalida!";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

                        break;
                }
            }
        }

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

                // COMANDOS ADMINISTRATIVOS

                if (payload.message.ToUpper() == "OK RST" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "ESP RESTARTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK RST" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "ESP RESTARTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
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

                // COMANDOS ADMINISTRATIVOS

                if (payload.message.ToUpper() == "OK RST" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "ESP RESTARTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK RST" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "ESP RESTARTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }
            }
        }

        public async Task HandleConfigTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                if (payload.message.ToUpper() == "OK RST" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "BROKER RESTARTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK RST" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "BROKER RESTARTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK STD" && payload.source.ToUpper() == "TELEGRAM")
                {
                    payload.message = "BROKER HALTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }

                if (payload.message.ToUpper() == "OK STD" && payload.source.ToUpper() == "SMS")
                {
                    payload.message = "BROKER HALTED";
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }
            }
        }



        public string PrepareMsgToBroker(Payload payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return jsonPayload;
        }

        public async Task PublishMqttClientAsync(string topic, string payload, bool retainFlag = false, int qos = 0)
        {
            _logger.LogInformation(string.Format("Enviando mensagem para o broker: {0}", payload));
            _logger.LogInformation(string.Format("Topico: {0}", topic));

            await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .WithRetainFlag(retainFlag)
                .Build());
        }
    }
}
