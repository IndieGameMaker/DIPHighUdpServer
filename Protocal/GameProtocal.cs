using System.Text.Json;

namespace HighUDPServer.Protocal;

// 게임 프로토콜 메시지 타입 정의
public enum MessageType : byte
{
    // 연결 관련 메시지
    Connect = 0x01, 
    ConnectResponse = 0x02,
    Disconnect = 0x03,
        
    // 플레이어 관련 메시지
    PlayerJoin = 0x10,
    PlayerLeave = 0x11,
        
    // 동기화 메시지
    TransformUpdate = 0x20, // 업데이트
    TransformSync = 0x21,   // 동기화
    
    // RPC 관련 메시진
    RpcCall = 0x30,             // RPC 호출
    RpcCallResponse = 0x31,     // RPC 응답
    
    // 시스템 메시지
    Heartbeat = 0x40,
    Echo = 0x41,
}

// 메시지 기본 구조체 : 공통 헤더 부분
public struct GameMessage
{
    public MessageType Type { get; set; } // 메시지 타입
    public string PlayerId { get; set; }  // 플레이어 ID
    public long Timestamp { get; set; }   // 메시지 생성시간
    public string Data { get; set; }
}

#region 메시지 데이터

// 연결 요청 메시지
public struct ConnectData
{
    public string PlayerName { get; set; }
}
    
// 연결 응답 메시지
public struct ConnectResponseData
{
    public bool Success { get; set; }    // 연결 성공 여부
    public string PlayerId { get; set; }
    public string Message { get; set; }  // 응답 메시지
}

// Transform 업데이트 메시지
public struct TransformData
{
    public Vector3 Position { get; set; }    // 새로운 위치
    public Vector3 Rotation { get; set; }    // 새로운 회전
    public float DeltaTime { get; set; }     // 이전 업데이트로부터의 시간 간격
}

/// <summary>
/// 플레이어 정보 구조체
/// </summary>
public struct PlayerData
{
    public string PlayerId { get; set; }     // 플레이어 ID
    public string PlayerName { get; set; }   // 플레이어 이름
    public Vector3 Position { get; set; }    // 현재 위치
    public Vector3 Rotation { get; set; }    // 현재 회전
    public long LastUpdate { get; set; }     // 마지막 업데이트 시간
}
#endregion

// 메시지 직렬화/역직렬화 처리
public static class GameProtocal
{
    // GameMessage를 JSON 바이트 배열로 직렬화 : string => byte[]
    public static byte[] Serialize(GameMessage message)
    {
        var json = JsonSerializer.Serialize(message);       // GameMessage를 JSON 문자열로 변환
        return System.Text.Encoding.UTF8.GetBytes(json);    // UTF-8 바이트 배열로 변환
    }
    
    // 역직렬화 : byte[] => string
    public static GameMessage Deserialize(byte[] data, int length)
    {
        var json = System.Text.Encoding.UTF8.GetString(data, 0, length); // 바이트 배열을 JSON 문자열로 변환
        return JsonSerializer.Deserialize<GameMessage>(json);   // JSON 문자열을 GameMessage 구조체로 역직렬화
    }

    // T타입 데이터를 포함하는 메시지 직렬화 생성 메서드
    public static GameMessage CreateMessage<T>(MessageType type, string playerId, T data)
    {
        return new GameMessage()
        {
            Type = type,
            PlayerId = playerId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // 현재 시간을 Unix 타임스템프로 설정
            Data = JsonSerializer.Serialize(data),
        };
    }
    
    // T타입 데이터를 추출하는 메소드(역직렬화)
    public static T? GetData<T>(GameMessage message)
    {
        return JsonSerializer.Deserialize<T>(message.Data);
    }
}
