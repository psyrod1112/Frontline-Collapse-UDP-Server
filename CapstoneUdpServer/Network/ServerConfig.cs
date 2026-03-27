
namespace CapstoneUdpServer.Network;

public class ServerConfig
{
    public string ServerIp { get; set; }
    public int Port { get; set; }
    public int MaxPlayerCounts { get; set; }
    public int BufferSize { get; set; }

    public ServerConfig()
    {
        ServerIp = "127.0.0.1";
        Port = 7777;
        MaxPlayerCounts = 100;
        BufferSize = 1024;
    }
    
}