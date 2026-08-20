namespace DeepSeekHarnessLauncher.Models;

/// <summary>端口占用事件参数。</summary>
public sealed class PortOccupiedEventArgs : EventArgs
{
    public PortOccupiedEventArgs(int port, int pid)
    {
        Port = port;
        Pid = pid;
    }

    public int Port { get; }
    public int Pid { get; }
}
