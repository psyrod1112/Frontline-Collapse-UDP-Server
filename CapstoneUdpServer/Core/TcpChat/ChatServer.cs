
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TcpChatServer;

public class ChatServer
{
    //연결되었는지 bool
    private bool _isRunning;
    
    //포트번호
    private readonly int _port;
    
    //리스너
    private TcpListener? _listener;

    //네트워크 프로그래밍에 딕셔너리 사용시 -> Thread Safe한 딕셔너리 사용 Concurrent
    private ConcurrentDictionary<string, ConnectedClient> _clients;

    public ChatServer(int port)
    {
        _port = port;
        _isRunning = false;
        _clients = new ConcurrentDictionary<string, ConnectedClient>();
    }

    public void Start()
    {
        if (_isRunning)
        {
            Console.WriteLine("서버가 이미 실행중입니다.");
            return;
        }

        _listener = new TcpListener(IPAddress.Any, _port);
        _isRunning = true;
        _listener.Start();
        Console.WriteLine($"서버를 실행합니다... (포트 번호: {_port})");

        _ = Task.Run(AcceptClientAsync);
    }

    //async Task(=동시에 여러개 하겠다)
    private async Task AcceptClientAsync()
    {
        try
        {
            Console.WriteLine("클라이언트 연결을 기다리는 중...");
            while (_isRunning)
            {
                //클라이언트 연결 수락
                var client = await _listener.AcceptTcpClientAsync();

                //접속된 클라이언트 저장 목록
                var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
                var connectedClient = new ConnectedClient(client); //Client
                _clients[clientId] = connectedClient;
                
                Console.WriteLine($"[연결] {clientId}와 연결되었습니다.");
                Console.WriteLine($"[정보] 현재 연결된 클라이언트 수 : {_clients.Count}");
                

                //클라이언트로부터 메시지 수신 시작
                _ = Task.Run(() => connectedClient.ReceiveMessageAsync()); //Client
                
                //채팅 서버에 메시지 수신되면 서버가 모든 클라이언트에게 브로드캐스트!!
                //메시지 수신 시 서버의 브로드캐스트를 위한 이벤트 연결
                connectedClient.MessageReceived += OnMessageReceived; //이벤트 선언만 Client
                connectedClient.Disconnected += OnClientDisconnected;


            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        
    }

    private void OnMessageReceived(ConnectedClient sender, string message)
    {
        
        //프로토콜 파싱
        if (message.StartsWith("CONNECTION:"))
        {
            string? nickName = message.Substring("CONNECTION:".Length);
            sender.NickName = nickName;
            Console.WriteLine($"[닉네임 설정] 클라이언트 {sender.ClientId}의 닉네임이 {nickName}으로 설정되었습니다.");
            return;

        }
        //브로드캐스팅할 메시지 생성
        
        if (message.StartsWith("CHAT:"))
        {
            string[] chatMsg = message.Split(":", 3);
            if (chatMsg.Length == 3)
            {
                string msg = $"[{chatMsg[1]}]: {chatMsg[2]}";
                
                //서버가 각각의 클라이언트들을 집합시킴
                _ = Task.Run(() => BroadcastMessageAsync(msg));
            }

            return;
        }
        
        if (message.StartsWith("ENTER:"))
        {
            string[] enterMsg = message.Split(":", 3);
            if (enterMsg.Length == 3)
            {
                string nickName = enterMsg[1];
                int inGameId = int.Parse(enterMsg[2]);

                var client = _clients.Values.FirstOrDefault(c => c.NickName == nickName);
                if (client != null)
                {
                    client.SwitchInGameId(inGameId);
                    Console.WriteLine($"[인게임 입장] {nickName}가 {inGameId}번 게임에 입장하였습니다.");
                }
                else
                {
                    Console.WriteLine($"[인게임 입장 실패] {nickName}를 찾을 수 없습니다.");
                }
            }
            return;
        }
        
        if (message.StartsWith("CHAT2:"))
        {
            string[] chatMsg = message.Split(":", 4);
            if (chatMsg.Length == 4)
            {
                int inGameId = int.Parse(chatMsg[1]);
                
                string msg =  $"[{chatMsg[2]}]: {chatMsg[3]}";

                _ = Task.Run(() => BroadcastInGameMessageAsync(msg, inGameId));
            }

            return;
        }
        
        
    }

    

    private void OnClientDisconnected(string clientId)
    {
        if (_clients.TryRemove(clientId, out var client))
        {
            client.MessageReceived -= OnMessageReceived;
            client.Disconnected -= OnClientDisconnected;
            Console.WriteLine($"[연결종료] 클라이언트 {client}가 연결이 종료되었습니다.");
            Console.WriteLine($"[정보] 현재 연결된 클라이언트 수: {_clients.Count}");
        }
    }

    private async Task BroadcastMessageAsync(string message)
    {
        foreach (var client in _clients.Values)
        {
            //로비 애들 끼리만 볼 수 있음.
            if (client.IsConnected && client.InGameId == -1)
            {
                //각각의 클라이언트들한테 보여짐
                await client.SendMessageAsync(message);
            }
        }
        Console.WriteLine($"[송신 완료] 플레이어들에게 {message}를 전달하였습니다.");
    }
    
    private async Task? BroadcastInGameMessageAsync(string msg, int inGameId)
    {
        foreach (var client in _clients.Values)
        {
            if (client.InGameId == inGameId)
            {
                await client.SendMessageAsync(msg);
                
            }
        }
        Console.WriteLine($"[송신 완료] {inGameId}번 게임의 플레이어들에게 {msg}를 전달하였습니다.");
    }


    public void Stop()
    {
        if (!_isRunning)
        {
            Console.WriteLine("서버가 이미 종료되었습니다.");
            return;
        }
        _isRunning = false;
        _listener?.Stop();
        Console.WriteLine($"서버가 종료되었습니다... (포트 번호: {_port})");
    }
}