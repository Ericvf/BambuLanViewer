using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BambuLanViewer.Services;

public class BambuCircuitHandler : CircuitHandler, IAsyncDisposable
{
    private readonly BambuMqttManager mqttConnectionManager;
    private readonly ILogger<BambuCircuitHandler> logger;

    private readonly string viewerId = Guid.NewGuid().ToString("N");
    private bool isViewing;
    private bool isDisposed;

    public event Action? ConnectionUp;
    public event Action? ConnectionDown;

    public bool IsViewer => isViewing;

    public BambuCircuitHandler(BambuMqttManager mqttManager, ILogger<BambuCircuitHandler> logger)
    {
        mqttConnectionManager = mqttManager;
        this.logger = logger;
    }

    public async Task StartViewingAsync()
    {
        if (isDisposed || isViewing)
            return;

        isViewing = true;
        await mqttConnectionManager.AddViewerAsync(viewerId);

        logger.LogInformation("Viewer {ViewerId} started viewing the printer.", viewerId);
    }

    public void StopViewing()
    {
        if (!isViewing)
            return;

        isViewing = false;
        mqttConnectionManager.RemoveViewer(viewerId);

        logger.LogInformation("Viewer {ViewerId} stopped viewing the printer.", viewerId);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Circuit connection restored: {CircuitId}", circuit.Id);

        if (isViewing)
        {
            _ = mqttConnectionManager.ReconnectedAsync(viewerId);
        }

        ConnectionUp?.Invoke();

        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Circuit connection lost: {CircuitId}", circuit.Id);

        ConnectionDown?.Invoke();

        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit,CancellationToken cancellationToken)
    {
        logger.LogInformation("Circuit closed: {CircuitId}",circuit.Id);

        StopViewing();

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (isDisposed)
            return ValueTask.CompletedTask;

        isDisposed = true;

        StopViewing();

        return ValueTask.CompletedTask;
    }
}