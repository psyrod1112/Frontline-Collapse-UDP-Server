using System;
using System.Net;
using CapstoneUdpServer.Network;

namespace CapstoneUdpServer.NetworkStream;

public class PacketJob:IJob
{
    
    private UdpServer _server;
    private byte[] _buffer;
    private int _bufferSize;
    private IPEndPoint _endPoint;

    public PacketJob(UdpServer server, byte[] buffer, int bufferSize, IPEndPoint endPoint)
    {
        _server = server;
        _buffer = buffer;
        _bufferSize = bufferSize;
        _endPoint = endPoint;
    }
    
    public void Execute()
    {
        try
        {
            //processPacket하기
            
            //TODO: 패킷 Scene타입에 따라 ProcessPacket이나 인게임 processPacket으로 나눠서 처리
            
            _server.ProcessPacket(_buffer, _bufferSize, _endPoint);
            
        }
        catch (Exception e)
        {
            Console.WriteLine("[서버] Execute: 오류 발생" + e.Message);
        }
        
    }
}