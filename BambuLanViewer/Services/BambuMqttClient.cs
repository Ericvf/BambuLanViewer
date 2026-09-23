using MQTTnet;
using MQTTnet.Formatter;
using System.Text;
using System.Text.Json.Nodes;

namespace BambuLanViewer.Services;

public class BambuMqttClient : IBambuMqttClient, IAsyncDisposable
{
    public event EventHandler<MqttClientConnectedEventArgs>? Connected;
    public event EventHandler<MqttClientDisconnectedEventArgs>? Disconnected;
    public event EventHandler<PrinterDataModel>? MessageReceived;

    private readonly ILogger<BambuMqttClient> logger;
    private readonly IConfiguration configuration;
    private readonly IMqttClient mqttClient;
    private readonly MqttClientOptions mqttClientOptions;

    private readonly string printerIp;
    private readonly string accessCode;
    private readonly string serialNumber;

    public bool IsConnected => mqttClient.IsConnected;

    public BambuMqttClient(ILogger<BambuMqttClient> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;

        var bambuSection = this.configuration.GetSection("BambuMqttClient");
        printerIp = bambuSection["IPAddress"] ?? throw new ArgumentException("Printer IP address is not configured");
        accessCode = bambuSection["AccessCode"] ?? throw new ArgumentException("Access code is not configured");
        serialNumber = bambuSection["SerialNumber"] ?? throw new ArgumentException("Serial number is not configured");

        var mqttFactory = new MqttClientFactory();
        mqttClient = mqttFactory.CreateMqttClient();

        mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(printerIp, 8883)
            .WithCredentials("bblp", accessCode)
            .WithTlsOptions(o => o
                .UseTls()
                .WithCertificateValidationHandler(_ => true)
            )
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession()
            .Build();

        mqttClient.ConnectedAsync += OnConnectedAsync;
        mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task ConnectAsync()
    {
        try
        {
            if (!IsConnected)
            {
                logger.LogInformation("Connecting to MQTT broker at {PrinterIp}...", printerIp);
                await mqttClient.ConnectAsync(mqttClientOptions);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to MQTT broker");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (IsConnected)
            {
                logger.LogInformation("Disconnecting from MQTT broker...");
                await mqttClient.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting from MQTT broker");
            throw;
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        logger.LogInformation("Successfully connected to MQTT broker");

        string topic = $"device/{serialNumber}/report";
        var topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
        await mqttClient.SubscribeAsync(topicFilter);
        logger.LogInformation("Subscribed to topic: {Topic}", topic);

        Connected?.Invoke(this, e);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Disconnected?.Invoke(this, e);
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            var jsonPayload = JsonNode.Parse(payload);
            var print = jsonPayload?["print"];

            if (print != null)
            {
                var printerData = new PrinterDataModel
                {
                    PrintPercentage = print["mc_percent"]?.GetValue<int>() ?? 0,
                    PrintLayer = print["layer_num"]?.GetValue<int>() ?? 0,
                    TotalLayers = print["total_layer_num"]?.GetValue<int>() ?? 0,
                    NozzleTemp = print["nozzle_temper"]?.GetValue<double>() ?? 0,
                    BedTemp = print["bed_temper"]?.GetValue<double>() ?? 0,
                    RemainingMinutes = print["mc_remaining_time"]?.GetValue<int>() ?? 0,
                    PrintObjectName = print["subtask_name"]?.GetValue<string>() ?? "",
                    PrintStatus = print?["gcode_state"]?.GetValue<string>() ?? ""
                };

                MessageReceived?.Invoke(this, printerData);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing received message");
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (IsConnected)
            {
                await DisconnectAsync();
            }

            mqttClient.ConnectedAsync -= OnConnectedAsync;
            mqttClient.DisconnectedAsync -= OnDisconnectedAsync;
            mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;

            mqttClient.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing MQTT service");
        }
    }
}
