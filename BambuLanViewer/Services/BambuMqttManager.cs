namespace BambuLanViewer.Services;

public class BambuMqttManager : IAsyncDisposable
{
    private readonly IBambuMqttClient mqttClient;
    private readonly ILogger<BambuMqttManager> logger;

    private readonly object synchronizationLock = new();

    private readonly HashSet<string> viewers = new();

    private CancellationTokenSource? disconnectToken;

    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(5);

    public BambuMqttManager(IBambuMqttClient mqtt, ILogger<BambuMqttManager> logger)
    {
        mqttClient = mqtt;
        this.logger = logger;
    }

    public bool HasViewers
    {
        get
        {
            lock (synchronizationLock)
            {
                return viewers.Count > 0;
            }
        }
    }

    public int ViewerCount
    {
        get
        {
            lock (synchronizationLock)
            {
                return viewers.Count;
            }
        }
    }

    public async Task AddViewerAsync(string circuitId)
    {
        bool shouldConnect;

        lock (synchronizationLock)
        {
            CancelPendingDisconnect();

            shouldConnect = viewers.Add(circuitId);

            logger.LogInformation("MQTT viewer added. Circuit: {CircuitId}. Active viewers: {ViewerCount}", circuitId, viewers.Count);
        }

        if (shouldConnect && !mqttClient.IsConnected)
        {
            await ConnectAsync();
        }
    }

    public void RemoveViewer(string circuitId)
    {
        bool lastViewer;

        lock (synchronizationLock)
        {
            viewers.Remove(circuitId);

            lastViewer = viewers.Count == 0;

            logger.LogInformation("MQTT viewer removed. Circuit: {CircuitId}. Active viewers: {ViewerCount}", circuitId, viewers.Count);

            if (lastViewer)
            {
                StartDisconnectTimer();
            }
        }
    }

    public async Task ReconnectedAsync(string circuitId)
    {
        bool isViewer;

        lock (synchronizationLock)
        {
            isViewer = viewers.Contains(circuitId);

            if (isViewer)
            {
                CancelPendingDisconnect();
            }
        }

        if (isViewer && !mqttClient.IsConnected)
        {
            await ConnectAsync();
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            if (mqttClient.IsConnected)
                return;

            await mqttClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to Bambu MQTT.");
        }
    }

    private void StartDisconnectTimer()
    {
        CancelPendingDisconnect();

        disconnectToken = new CancellationTokenSource();

        _ = DisconnectAfterTimeoutAsync(disconnectToken.Token);
    }

    private async Task DisconnectAfterTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DisconnectTimeout, cancellationToken);

            lock (synchronizationLock)
            {
                var shouldDisconnect = viewers.Count == 0 && !cancellationToken.IsCancellationRequested;

                if (!shouldDisconnect)
                    return;

                if (!mqttClient.IsConnected)
                    return;
            }

            logger.LogInformation("No MQTT viewers remain after {Timeout} seconds. Disconnecting.", DisconnectTimeout.TotalSeconds);
            await mqttClient.DisconnectAsync();
        }
        catch (OperationCanceledException)
        {
            // Disconnect was cancelled
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting Bambu MQTT.");
        }
    }

    private void CancelPendingDisconnect()
    {
        if (disconnectToken is null)
            return;

        disconnectToken.Cancel();
        disconnectToken.Dispose();
        disconnectToken = null;
    }

    public async ValueTask DisposeAsync()
    {
        lock (synchronizationLock)
        {
            viewers.Clear();
            CancelPendingDisconnect();
        }

        if (mqttClient.IsConnected)
        {
            try
            {
                await mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error disconnecting Bambu MQTT during application shutdown.");
            }
        }
    }
}