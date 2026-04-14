using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace TcpChatServer;

public class ConnectedClient : IDisposable
{
    //TCP 클라이언트 받아오기 _client
    private readonly TcpClient _client;
    
    //네트워크 스트림 _stream
    private readonly NetworkStream _stream;
    
    //클라이언트 ID관리(string)
    private readonly string? _clientId;
    
    //resource 처리 여부 _isDisposed
    private bool _isDisposed;
    
    //닉네임
    private string _nickName;
    
    //인게임 정보
    private int _inGameId = -1;
    
    //송/수신용 스트림
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    
    //메시지 수신 이벤트
    public event Action<ConnectedClient, string> MessageReceived;
    public event Action<string> Disconnected;
    
    
    #region 프로퍼티
    public string? ClientId => _clientId;
    public bool IsConnected => _client.Connected && !_isDisposed;
    
    public int InGameId => _inGameId;
    
    public string NickName { get; set; }
    
    #endregion

    #region 생성자

    public ConnectedClient(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
        
        //스트림 초기화 -> networkStream의 바이트 코드를 인코딩하기 위함
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    #endregion
    
    #region 변수변경 메서드

    public void SwitchInGameId(int inGameId)
    {
        _inGameId = inGameId;
    }
    
    #endregion
    
    #region 메소드
    
    //비동기 메시지 수신
    public async Task ReceiveMessageAsync()
    {
        try
        {
            while (IsConnected)
            {
                string? message = await _reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine($"[연결 종료] {_clientId}의 연결이 종료되었습니다.");
                    break;
                }
                Console.WriteLine($"[수신] {_clientId}: {message}");

                MessageReceived?.Invoke(this, message);
            }
        }
        catch (Exception e)
        {
            if (!_isDisposed)
            {
                Console.WriteLine("[전송 오류] : 메시지 입력에 실패하였습니다!");
            }
        }
        finally
        {
            Dispose();
        }
        
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            Console.WriteLine("이미 Dispose되었습니다...");
            Console.Write("아무 키나 입력하세요...");
            Console.ReadKey(true);
            return;
        }
        _isDisposed = true;
        
        Disconnected?.Invoke(_clientId);
        
        _stream.Dispose();
        _client.Dispose();
        _reader.Dispose();
        _writer.Dispose();
    }
    
    public async Task SendMessageAsync(string message)
    {
        if (!IsConnected)
        {
            Console.WriteLine("[전송 실패] 클라이언트 전송 불가");
            return;
        }
        try
        {
            _writer.WriteLineAsync(message);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[전송 오류] 메시지 전송 실패: {e.Message}");
        }
        
        
    }
    
    #endregion

    
    
    
}