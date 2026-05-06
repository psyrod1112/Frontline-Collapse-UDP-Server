using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CapstoneUdpServer.Data;
using CapstoneUdpServer.Network;
using CapstoneUdpServer.NetworkStream;

namespace CapstoneUdpServer.Core;

/// <summary>
/// UDP 인프라 담당 (소켓, 수신 루프, 워커 스레드, JobQueue).
/// 패킷 처리는 LobbyServer / InGameServer에 위임.
/// </summary>
public class UdpServer : IDisposable
{
    private Socket _socket;
    private readonly ServerConfig _config;
    private bool _isRunning;
    private Thread[] _workerThreads;
    private const int WORKER_THREAD_COUNT = 10;
    private readonly JobQueue _jobQueue = new();
    private bool _disposed;

    // ── 공유 상태 (LobbyServer, InGameServer 모두 참조) ──────────
    internal readonly PlayerStore                            PlayerStore    = new();
    internal readonly ConcurrentDictionary<int, InGameData>  InGameDataList = new();

    public LobbyServer   LobbyServer   { get; private set; }
    public InGameServer  InGameServer  { get; private set; }
    public bool          IsRunning     => _isRunning;

    public UdpServer(ServerConfig config)
    {
        _config = config;
    }

    // ─────────────────────────────────────────────────────────────
    #region 초기화 / 시작

    public async Task<bool> Initialize()
    {
        try
        {
            var dbManager   = new DbManager();
            bool dbOk       = await dbManager.ConnectDBAsync();
            bool redisOk    = await dbManager.ConnectRedisAsync();
            if (!dbOk || !redisOk)
            {
                Console.WriteLine("[UdpServer] Initialize: DB/Redis 연결 실패");
                return false;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Bind(new IPEndPoint(IPAddress.Parse(_config.ServerIp), _config.Port));

            LobbyServer  = new LobbyServer(_socket, PlayerStore, InGameDataList, dbManager);
            InGameServer = new InGameServer(_socket, InGameDataList, PlayerStore);
            LobbyServer.SetInGameServer(InGameServer);

            Console.WriteLine("[UdpServer] Initialize: 초기 설정 완료");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[UdpServer] Initialize 실패: {e.Message}");
            return false;
        }
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            Console.WriteLine("[UdpServer] 이미 실행 중입니다.");
            return;
        }
        _isRunning = true;
        StartWorkerThreads();
        await Task.Run(ReceiveLoop);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 워커 스레드

    private void StartWorkerThreads()
    {
        _workerThreads = new Thread[WORKER_THREAD_COUNT];
        for (int i = 0; i < WORKER_THREAD_COUNT; i++)
        {
            int id = i;
            _workerThreads[i] = new Thread(() => WorkerLoop(id)) { IsBackground = true };
            _workerThreads[i].Start();
        }
        Console.WriteLine($"[UdpServer] 워커 스레드 {WORKER_THREAD_COUNT}개 실행 중");
    }

    private void WorkerLoop(int threadId)
    {
        while (_isRunning)
            _jobQueue.Dequeue().Execute();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 수신 루프

    private void ReceiveLoop()
    {
        byte[]   buffer   = ArrayPool<byte>.Shared.Rent(_config.BufferSize);
        EndPoint clientEp = new IPEndPoint(IPAddress.Any, _config.Port);
        try
        {
            while (_isRunning)
            {
                try
                {
                    int len = _socket.ReceiveFrom(buffer, ref clientEp);
                    if (len > 0)
                    {
                        byte[] packet = new byte[len];
                        Array.Copy(buffer, packet, len);
                        _jobQueue.Enqueue(new PacketJob(LobbyServer, InGameServer, packet, len, (IPEndPoint)clientEp));
                    }
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Windows ICMP "Port Unreachable": 클라이언트 소켓이 닫힌 후 발생하는 정상적인 현상
                    // 무시하고 루프 계속
                    Console.WriteLine($"[UdpServer] 클라이언트 연결 종료 감지 (ICMP) — 루프 유지");
                }
                catch (SocketException se)
                {
                    Console.WriteLine("[UdpServer] ReceiveLoop 소켓 오류: " + se.Message);
                    break; // 진짜 소켓 오류면 루프 종료
                }
            }
        }
        catch (Exception e) { Console.WriteLine("[UdpServer] ReceiveLoop 오류: " + e.Message); }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Dispose();
        }
        Console.WriteLine("[UdpServer] ReceiveLoop 종료");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _isRunning = false;
        InGameServer?.Stop();
        LobbyServer?.Dispose();
        _socket?.Dispose();
        PlayerStore.Clear();
        InGameDataList.Clear();
        _disposed = true;
    }
}
