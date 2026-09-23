using MQTTnet;

namespace BambuLanViewer.Services;

public interface IBambuMqttClient
{
    event EventHandler<MqttClientConnectedEventArgs>? Connected;

    event EventHandler<MqttClientDisconnectedEventArgs>? Disconnected;

    event EventHandler<PrinterDataModel>? MessageReceived;

    bool IsConnected { get; }

    Task ConnectAsync();

    Task DisconnectAsync();
}


public class PrinterDataModel
{
    public int PrintPercentage { get; set; }
    public int PrintLayer { get; set; }
    public int TotalLayers { get; set; }
    public double NozzleTemp { get; set; }
    public double BedTemp { get; set; }
    public int RemainingMinutes { get; set; }
    public string PrintObjectName { get; set; } = string.Empty;
    public string PrintStatus { get; set; } = string.Empty;
}
