using CapstoneUdpServer.Network;
using TcpChatServer;

namespace CapstoneUdpServer.Core;

/// <summary>
/// 통합 서버 진입점.
/// ─ UDP 서버(게임 로직) 가 주(主), TCP 채팅 서버가 종(從).
/// ─ UDP Initialize() 가 성공해야만 TCP 서버를 시작한다.
///   UDP 초기화 실패 시 TCP 서버는 절대 올라오지 않는다.
/// ─ TCP · UDP 모두 포트 8888 사용 (프로토콜이 달라 충돌 없음).
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding  = System.Text.Encoding.UTF8;

        // ── 1. UDP 서버(주) 초기화 ────────────────────────────────
        ServerConfig config    = new ServerConfig();
        UdpServer    udpServer = new UdpServer(config);

        bool udpReady = await udpServer.Initialize();
        if (!udpReady)
        {
            Console.WriteLine("[통합서버] UDP 초기화 실패 → TCP 서버도 시작하지 않습니다. 강제종료.");
            return;
        }
        Console.WriteLine("[통합서버] UDP 서버 초기화 완료.");

        // ── 2. TCP 채팅 서버(종) 시작 — UDP 성공 후에만 실행 ──────
        ChatServer tcpServer = new ChatServer(config.Port);
        tcpServer.Start();
        Console.WriteLine($"[통합서버] TCP 채팅 서버 시작 (포트 {config.Port}).");

        // ── 3. 두 서버 동시 실행 ─────────────────────────────────
        //   UDP: 게임 로직 전담 (수신 루프 + 워커 스레드)
        //   TCP: 채팅 전담 (AcceptClientAsync 내부에서 비동기 처리)
        Console.WriteLine("[통합서버] UDP + TCP 통합 서버 가동 중... (Ctrl+C 로 종료)");
        try
        {
            await Task.Run(udpServer.StartAsync); // 블로킹: UDP 수신 루프
        }
        catch (Exception e)
        {
            Console.WriteLine("[통합서버] 오류 발생: " + e.Message);
        }
        finally
        {
            tcpServer.Stop();
            udpServer.Dispose();
            Console.WriteLine("[통합서버] 종료 완료.");
        }
    }
}
