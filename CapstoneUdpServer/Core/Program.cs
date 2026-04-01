
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
            if (!await server.Initialize())
            {
                Console.WriteLine("[서버] Initialize 오류 발생! 강제종료");
                return;
            }

            await Task.Run(server.StartAsync);
        }
        catch (Exception e)
        {
            Console.WriteLine("[서버] Main 함수 오류! " + e.Message);
        }

    }
}
