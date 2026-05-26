
using System;

namespace CapstoneUdpServer.Network;

public class ServerConfig
{
    public string ServerIp { get; set; }
    public int Port { get; set; }
    public int MaxPlayerCounts { get; set; }
    public int BufferSize { get; set; }

    public ServerConfig()
    {
        ServerIp = Environment.GetEnvironmentVariable("SERVER_IP") ?? "0.0.0.0";
        Port = 8888;
        MaxPlayerCounts = 100;
        BufferSize = 1024;
    }
    
}