
using System;
using System.Threading.Tasks;
using CapstoneUdpServer.Network;

namespace CapstoneUdpServer.Core;

class Program
{
    async static Task Main(string[] args)
    {
        try
        {
            ServerConfig config = new ServerConfig();
            UdpServer server = new UdpServer(config);
            server.Initialize();

            await Task.Run(server.StartAsync);
        }
        catch (Exception e)
        {
            Console.WriteLine("[서버] Main 함수 오류! " + e.Message);
        }
        
    }
}