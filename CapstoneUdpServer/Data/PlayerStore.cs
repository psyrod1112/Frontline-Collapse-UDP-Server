using System.Collections.Concurrent;
using System.Net;
using CapstoneUdpServer.Data;

namespace CapstoneUdpServer.Core;

/// <summary>
/// 로비/인게임 플레이어 pool을 분리 관리.
/// 연결(소켓)은 유지하면서 서버 내 데이터 위치만 이동.
/// </summary>
public class PlayerStore
{
    private readonly ConcurrentDictionary<int, PlayerData> _lobbyPlayers  = new();
    private readonly ConcurrentDictionary<int, PlayerData> _inGamePlayers = new();

    public IEnumerable<PlayerData> LobbyPlayers  => _lobbyPlayers.Values;
    public IEnumerable<PlayerData> InGamePlayers => _inGamePlayers.Values;

    // ── 조회 ────────────────────────────────────────────────────

    public bool TryGetLobby(int id, out PlayerData p)  => _lobbyPlayers.TryGetValue(id, out p);
    public bool TryGetInGame(int id, out PlayerData p) => _inGamePlayers.TryGetValue(id, out p);

    /// <summary>어느 pool에 있든 상관없을 때 (ex. 연결 해제)</summary>
    public bool TryGet(int id, out PlayerData p) =>
        _lobbyPlayers.TryGetValue(id, out p) || _inGamePlayers.TryGetValue(id, out p);

    // ── 로비 pool 조작 ──────────────────────────────────────────

    public void AddToLobby(int id, PlayerData p) => _lobbyPlayers[id] = p;

    public bool RemoveFromLobby(int id) => _lobbyPlayers.TryRemove(id, out _);

    // ── pool 전환 (연결 유지, 데이터만 이동) ────────────────────

    /// <summary>로비 → 인게임: 게임 시작 시 호출</summary>
    public bool MoveToInGame(int id)
    {
        if (!_lobbyPlayers.TryRemove(id, out var p)) return false;
        _inGamePlayers[id] = p;
        return true;
    }

    /// <summary>인게임 → 로비: 게임 종료 후 복귀 시 호출</summary>
    public bool MoveToLobby(int id)
    {
        if (!_inGamePlayers.TryRemove(id, out var p)) return false;
        p.IsGameReady = false;
        _lobbyPlayers[id] = p;
        return true;
    }

    /// <summary>연결 끊김 시 어느 pool이든 제거</summary>
    public bool Remove(int id) =>
        _lobbyPlayers.TryRemove(id, out _) || _inGamePlayers.TryRemove(id, out _);

    public void Clear()
    {
        _lobbyPlayers.Clear();
        _inGamePlayers.Clear();
    }
}
