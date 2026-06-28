using System.IO.Ports;
using RobsWindSim.Models;

namespace RobsWindSim.Services;

public sealed class SerialFanController : IDisposable
{
    private const int BaudRate = 115200;
    private const int PwmMax = 511;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan BootSettleDelay = TimeSpan.FromMilliseconds(1500);

    private readonly object _lock = new();
    private SerialPort? _port;
    private string _configuredPort = string.Empty;
    private DateTime _nextConnectAttemptUtc = DateTime.MinValue;
    private DateTime _suppressSendUntilUtc = DateTime.MinValue;
    private int _consecutiveWriteFailures;

    public bool IsConnected
    {
        get
        {
            lock (_lock)
                return _port?.IsOpen == true && DateTime.UtcNow >= _suppressSendUntilUtc;
        }
    }

    public string ConfiguredPort
    {
        get
        {
            lock (_lock)
                return _configuredPort;
        }
    }

    public void ConfigurePort(string? comPort)
    {
        lock (_lock)
        {
            var port = comPort ?? string.Empty;
            if (string.Equals(_configuredPort, port, StringComparison.OrdinalIgnoreCase))
                return;

            _configuredPort = port;
            DisconnectInternal();
            _nextConnectAttemptUtc = DateTime.MinValue;
        }
    }

    public void RequestImmediateSend()
    {
        // Next Tick sends immediately once connected.
    }

    public void Tick(FanOutput output)
    {
        lock (_lock)
        {
            EnsureConnected();
            if (_port?.IsOpen != true || DateTime.UtcNow < _suppressSendUntilUtc)
                return;

            try
            {
                DrainReceiveBuffer();

                var leftDuty = PercentToDuty(output.LeftPercent);
                var rightDuty = PercentToDuty(output.RightPercent);
                _port.Write($"PWM {leftDuty} {rightDuty}\n");
                _consecutiveWriteFailures = 0;
            }
            catch (IOException)
            {
                _consecutiveWriteFailures++;
                if (_consecutiveWriteFailures >= 3)
                {
                    DisconnectInternal();
                    _nextConnectAttemptUtc = DateTime.UtcNow.Add(ReconnectDelay);
                }
            }
            catch (UnauthorizedAccessException)
            {
                DisconnectInternal();
                _nextConnectAttemptUtc = DateTime.UtcNow.Add(ReconnectDelay);
            }
        }
    }

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames().OrderBy(p => p).ToArray();

    public void Dispose()
    {
        lock (_lock)
            DisconnectInternal();
    }

    private void EnsureConnected()
    {
        if (string.IsNullOrWhiteSpace(_configuredPort))
            return;

        if (_port?.IsOpen == true)
            return;

        if (DateTime.UtcNow < _nextConnectAttemptUtc)
            return;

        DisconnectInternal();

        try
        {
            _port = new SerialPort(_configuredPort, BaudRate)
            {
                NewLine = "\n",
                ReadTimeout = 50,
                WriteTimeout = 500,
                DtrEnable = true,
                RtsEnable = true
            };
            _port.Open();
            _suppressSendUntilUtc = DateTime.UtcNow.Add(BootSettleDelay);
            _consecutiveWriteFailures = 0;
            DrainReceiveBuffer();
        }
        catch
        {
            DisconnectInternal();
            _nextConnectAttemptUtc = DateTime.UtcNow.Add(ReconnectDelay);
        }
    }

    private void DrainReceiveBuffer()
    {
        if (_port == null || !_port.IsOpen || _port.BytesToRead <= 0)
            return;

        try
        {
            _port.ReadExisting();
        }
        catch (IOException)
        {
            // Ignore read errors while draining.
        }
    }

    private void DisconnectInternal()
    {
        if (_port == null)
            return;

        try
        {
            if (_port.IsOpen)
                _port.Close();
        }
        catch
        {
            // ignore cleanup errors
        }
        finally
        {
            _port.Dispose();
            _port = null;
        }
    }

    private static int PercentToDuty(double percent) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) / 100.0 * PwmMax);
}
