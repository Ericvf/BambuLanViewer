using MQTTnet;
using MQTTnet.Formatter;
using System.Text;
using System.Text.Json.Nodes;

namespace BambuLanViewer.Services;

public class BambuMttqClient : IBambuMttqClient, IAsyncDisposable
{
    public event EventHandler<MqttClientConnectedEventArgs>? Connected;
    public event EventHandler<MqttClientDisconnectedEventArgs>? Disconnected;
    public event EventHandler<PrinterDataModel>? MessageReceived;

    private readonly ILogger<BambuMttqClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _mqttClientOptions;

    private readonly string printerIp;
    private readonly string accessCode;
    private readonly string serialNumber;

    public bool IsConnected => _mqttClient.IsConnected;

    public BambuMttqClient(ILogger<BambuMttqClient> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        var bambuSection = _configuration.GetSection("BambuMttqClient");
        printerIp = bambuSection["IPAddress"] ?? throw new ArgumentException("Printer IP address is not configured");
        accessCode = bambuSection["AccessCode"] ?? throw new ArgumentException("Access code is not configured");
        serialNumber = bambuSection["SerialNumber"] ?? throw new ArgumentException("Serial number is not configured");

        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();

        _mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(printerIp, 8883)
            .WithCredentials("bblp", accessCode)
            .WithTlsOptions(o => o
                .UseTls()
                .WithCertificateValidationHandler(_ => true)
            )
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession()
            .Build();

        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task ConnectAsync()
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogInformation("Connecting to MQTT broker at {PrinterIp}...", printerIp);
                await _mqttClient.ConnectAsync(_mqttClientOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to MQTT broker");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (IsConnected)
            {
                _logger.LogInformation("Disconnecting from MQTT broker...");
                await _mqttClient.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from MQTT broker");
            throw;
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogInformation("Successfully connected to MQTT broker");

        string topic = $"device/{serialNumber}/report";
        var topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
        await _mqttClient.SubscribeAsync(topicFilter);
        _logger.LogInformation("Subscribed to topic: {Topic}", topic);

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
            _logger.LogError(ex, "Error processing received message");
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

            _mqttClient.ConnectedAsync -= OnConnectedAsync;
            _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;

            _mqttClient.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MQTT service");
        }
    }

}
