using System.Net;
using System.Text;
using System.Text.Json;
using CapstoneUdpServer.Core;
using CapstoneUdpServer.Data;
using CapstoneUdpServer.Network;
using static CapstoneUdpServer.Network.JsonOpts;

namespace CapstoneUdpServer.NetworkStream;

public class PacketJob : IJob
{
    private readonly LobbyServer  _lobbyServer;
    private readonly InGameServer _inGameServer;
    private readonly byte[]       _buffer;
    private readonly int          _bufferSize;
    private readonly IPEndPoint   _endPoint;

    public PacketJob(LobbyServer lobbyServer, InGameServer inGameServer,
                     byte[] buffer, int bufferSize, IPEndPoint endPoint)
    {
        _lobbyServer  = lobbyServer;
        _inGameServer = inGameServer;
        _buffer       = buffer;
        _bufferSize   = bufferSize;
        _endPoint     = endPoint;
    }

    public void Execute()
    {
        try
        {
            string      jsonData = Encoding.UTF8.GetString(_buffer, 0, _bufferSize);
            BasePacket? header   = JsonSerializer.Deserialize<BasePacket>(jsonData, Default);

            if (header?.Type == LobbyPacketType.None)
                _inGameServer.ProcessPacket(_buffer, _bufferSize, _endPoint);
            else
                _lobbyServer.ProcessPacket(_buffer, _bufferSize, _endPoint);
        }
        catch (Exception e)
        {
            Console.WriteLine("[PacketJob] Execute 오류: " + e.Message);
        }
    }
}
