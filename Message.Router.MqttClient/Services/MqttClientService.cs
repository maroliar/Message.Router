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
using System.Collections.Generic;
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

        private Dictionary<string, FlagParams> flagsMenu = new Dictionary<string, FlagParams>();

        private readonly string menu = "Home Automation"
            + "\r\n\r\nDigite:"
            + "\r\n1 para Temperatura Raspberry"
            + "\r\n2 para Desodorizar Ambiente "
            + "\r\n3 para Abrir Portaria "
            + "\r\n4 para Alimentar Pets"
            + "\r\n5 para Gravar Nova Tarefa";

        private readonly string adminMenu = "Home Automation - Admin"
            + "\r\n\r\nDigite:"
            + "\r\n1 para Restart PETS"
            + "\r\n2 para Restart INTERFONE"
            + "\r\n3 para Restart DESODORIZACAO"
            + "\r\n4 para Resrart GatewaySMS"
            + "\r\n5 para Restart Raspberry"
            + "\r\n6 para Shutdown Raspberry"
            + "\r\n\r\nVOLTAR para Voltar ao Menu";

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

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoConfig).Build());
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(brokerTopics.TopicoTasks).Build());
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
                    ReasonString = "NormalDisconnection"
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

                        payload.message = "GET";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoTemperatura, serializedPayload);

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


                    // OPÇÕES ADMINISTRATIVAS, OCULTAS POR DEFAULT AO MENU

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

                    case "RST DES": // RESTART DESODORIZADOR

                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);

                        break;

                    case "RST SMS": // RESTART GATEWAY SMS // INVIAVEL A RESPOSTA DO DEVICE..

                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMS, serializedPayload);

                        break;


                    // OPÇÕES ADMINISTRATIVAS DO BROKER, OCULTAS POR DEFAULT AO MENU, E COM RESPOSTA HARDCODED NO SCRIPT brokerConfig.sh

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

                if (!flagsMenu.ContainsKey(payload.device))
                {
                    flagsMenu.Add(payload.device, new FlagParams { FlagAdminMenu = false });
                }

                payload.message = payload.message.ToUpper().Trim();


                // ADMIN MENU

                if (flagsMenu[payload.device].FlagAdminMenu)
                {
                    if (payload.message == "MENU")         // EXIBIR MENU ADMIN
                    {
                        payload.message = adminMenu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }

                    else if (payload.message == "VOLTAR")  // VOLTAR MENU PRINICPAL
                    {
                        if (flagsMenu.ContainsKey(payload.device))
                        {
                            flagsMenu[payload.device].FlagAdminMenu = false;
                        }

                        payload.message = menu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }

                    else if (payload.message == "1")       // RESTART ESP32 PETS
                    {
                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);
                    }

                    else if (payload.message == "2")       // RESTART NODEMCU INTERFONE
                    {
                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);
                    }

                    else if (payload.message == "3")       // RESTART ESP8266 DESODORIZACAO
                    {
                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);
                    }

                    else if (payload.message == "4")       // RESTART ESP32 GATEWAYSMS
                    {
                        payload.message = "RST";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMS, serializedPayload);
                    }

                    else if (payload.message == "5")       // RESTART BROKER RASPBERRYPI
                    {
                        payload.message = "RESTART";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoConfig, serializedPayload);
                    }

                    else if (payload.message == "6")       // SHUTDOWN BROKER RASPBERRYPI
                    {
                        payload.message = "SHUTDOWN";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoConfig, serializedPayload);
                    }

                    else
                    {
                        payload.message = "Opcao Invalida! \r\nDigite MENU para ver as Opções";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }
                }


                // MENU PRINCIPAL

                else
                {
                    if (payload.message == "MENU")         // EXIBIR MENU PRINCIPAL
                    {
                        payload.message = menu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }

                    else if (payload.message == "1")       // EXIBIR TEMPERATURA
                    {
                        payload.message = "GET";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoTemperatura, serializedPayload);
                    }

                    else if (payload.message == "2")       // DESODORIZAR AMBIENTE
                    {
                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoDesodorizacao, serializedPayload);
                    }

                    else if (payload.message == "3")       // ABRIR PORTARIA
                    {
                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoInterfone, serializedPayload);
                    }

                    else if (payload.message == "4")       // ALIMENTAR PETS
                    {
                        payload.message = "ACT";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoPets, serializedPayload);
                    }

                    else if (payload.message == "5")       // EXIBIR OPCAO DE ALERTA
                    {
                        payload.message = "Digite a nova tarefa seguido do tempo em minutos, conforme exemplo abaixo: " + "\r\n\r\nALERT Jogar o Lixo Fora, 30";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }

                    else if (payload.message.StartsWith("ALERT")) // GRAVAR NOVO ALERTA
                    {
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoTasks, serializedPayload);

                        payload.message = "Tarefa Gravada! Voce deverá receber uma notificação no tempo escolhido.";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }

                    else if (payload.message == "ADMIN")   // ENTRAR NO MENU ADMIN
                    {
                        if (flagsMenu.ContainsKey(payload.device))
                        {
                            flagsMenu[payload.device].FlagAdminMenu = true;
                        }

                        payload.message = adminMenu;
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }

                    else
                    {
                        payload.message = "Opcao Invalida! \r\nDigite MENU para ver as Opções";
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }
                }
            }
        }

        public async Task HandleTemperaturaTopicAsync(Payload payload)
        {
            if (!string.IsNullOrEmpty(payload.message))
            {
                string serializedPayload;

                int? temp = Convert.ToInt32(payload.message);

                if (payload.source.ToUpper() == "TELEGRAM")
                {
                    if (temp > 0 && temp < 80) // provavel origem de solicitação do usuario
                    {
                        payload.message = string.Format("Temperatura do RaspBerry: {0} graus.", payload.message);
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }
                    else if (temp >= 80)       // provavel origem do script de alertRaspberryTemperature
                    {
                        payload.message = string.Format("Temperatura do RaspBerry muito alta! {0} graus.", payload.message);
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewayTelegramSaida, serializedPayload);
                    }
                }

                if (payload.source.ToUpper() == "SMS")
                {
                    if (temp > 0 && temp < 80) // provavel origem de solicitação do usuario
                    {
                        payload.message = string.Format("Temperatura do RaspBerry: {0} graus.", payload.message);
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                    }
                    else if (temp >= 80)       // provavel origem do script de alertRaspberryTemperature
                    {
                        payload.message = string.Format("Temperatura do RaspBerry muito alta! {0} graus.", payload.message);
                        serializedPayload = PrepareMsgToBroker(payload);
                        await PublishMqttClientAsync(brokerTopics.TopicoGatewaySMSSaida, serializedPayload);
                    }
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

        private void CheckFlagParams(string device)
        {


        }
    }
}
