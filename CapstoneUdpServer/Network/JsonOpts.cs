using System.Text.Json;

namespace CapstoneUdpServer.Network;

/// <summary>
/// Unity JsonUtility는 필드명을 소문자로 직렬화 (x, y, z).
/// System.Text.Json 기본값은 대문자 (X, Y, Z).
/// → CamelCase 정책으로 직렬화, 역직렬화 시 대소문자 무시.
/// </summary>
public static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
