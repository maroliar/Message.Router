using Message.Router.MqttClient.Entities;
using Message.Router.MqttClient.Settings;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using MQTTnet.Server;
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
            + "\r\nOP2 - Desodorizar Ambiente "
            + "\r\nOP3 - Abrir Portaria "
            + "\r\nOP4 - Alimentar Pets"
            + "\r\nOP5 - Rastreio Encomenda";

        private bool flagSubMenuRastreioEncomendasAtivo;
        private readonly string subMenuRastreioEncomendas = "Por favor, digite o codigo de rastreio, de 13 digitos de sua encomenda."
            + "\r\nPara voltar ao Menu anterior, digite MENU: ";


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

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoRastreioEncomendas).Build());

        }

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

                if (!string.IsNullOrEmpty(payload.message))
                {
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

                    if (topic.Contains(brokerTopics.TopicoRastreioEncomendas))
                    {
                        await HandleRastreioEncomendasTopicAsync(payload);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao tentar ler a mensagem: " + ex.Message);
            }
        }

        public async Task HandleSMSTopicAsync(Payload payload)
        {
            string serializedPayload;

            if (flagSubMenuRastreioEncomendasAtivo && payload.message.ToUpper() != "MENU")
            {
                if(payload.message.Length == 13)
                {
                    flagSubMenuRastreioEncomendasAtivo = false;
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoRastreioEncomendas, serializedPayload);
                }
                else
                {
                    payload.message = "Codigo de Rastreio Invalido!" 
                        + "\r\n" 
                        + subMenuRastreioEncomendas;
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                }              
            }
            else
            {
                switch (payload.message.ToUpper())
                {
                    case "MENU":

                        flagSubMenuRastreioEncomendasAtivo = false;
                        payload.message = menu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);

                        break;

                    case "OP2":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "OP3":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "OP4":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;

                    case "OP5":

                        flagSubMenuRastreioEncomendasAtivo = true;
                        payload.message = subMenuRastreioEncomendas;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);

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
            string serializedPayload;

            if (flagSubMenuRastreioEncomendasAtivo && payload.message.ToUpper() != "MENU")
            {
                if (payload.message.Length == 13)
                {
                    flagSubMenuRastreioEncomendasAtivo = false;
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoRastreioEncomendas, serializedPayload);
                }
                else
                {
                    payload.message = "Codigo de Rastreio Invalido!"
                        + "\r\n"
                        + subMenuRastreioEncomendas;
                    serializedPayload = PrepareMsgToBroker(payload);
                    await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                }
            }
            else
            {
                switch (payload.message.ToUpper())
                {
                    case "MENU":

                        flagSubMenuRastreioEncomendasAtivo = false;
                        payload.message = menu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

                        break;

                    case "OP2":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "OP3":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);

                        break;

                    case "OP4":

                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);

                        break;

                    case "OP5":

                        flagSubMenuRastreioEncomendasAtivo = true;
                        payload.message = subMenuRastreioEncomendas;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);

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
            string serializedPayload;

            // Por enquanto, apenas envia notificações pelo Telegram, não por SMS
            if (int.TryParse(payload.message, out _) && payload.source.ToUpper() == "TELEGRAM")
            {
                payload.message = "Temperatura do Raspberry muito alta! " + payload.message;
                serializedPayload = PrepareMsgToBroker(payload);
                await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
            }
        }

        public async Task HandleDesodorizacaoTopicAsync(Payload payload)
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

        public async Task HandleInterfoneTopicAsync(Payload payload)
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

        public async Task HandlePetsTopicAsync(Payload payload)
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

        public async Task HandleRastreioEncomendasTopicAsync(Payload payload)
        {
            string serializedPayload;

            if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "TELEGRAM")
            {
                payload.message = "Solicitação efetuada com sucesso! \r\nQuando a encomenda Sair para Entrega, voce recebera uma notificacao.";
                serializedPayload = PrepareMsgToBroker(payload);
                await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
            }

            if (payload.message.ToUpper() == "OK" && payload.source.ToUpper() == "SMS")
            {
                payload.message = "Solicitação efetuada com sucesso! \r\nQuando a encomenda Sair para Entrega, voce recebera uma notificacao.";
                serializedPayload = PrepareMsgToBroker(payload);
                await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
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
