using System.Collections.Concurrent;
using System.Net;
using HighUDPServer;
using HighUDPServer.Protocal;

// 플레이어 연결, 해제, 상태 관리 기능 제공
public class ClientManager
{
    // 플레이어 ID를 키로 하는 클라이언트 정보 딕셔너리
    private readonly ConcurrentDictionary<string, ClientInfo> _clients;
    
    // EndPoint를 키로 하는 플레이어 ID 매핑용 딕셔너리
    private readonly ConcurrentDictionary<string, string> _endPointToPlayerId;
    
    // 플레이어 ID 생성 시 동기화를 위한 락
    private readonly object _playerIdLock = new object();
    // 다음 플레이어 ID (1부터 시작)
    private int _nextPlayerId = 1; 

    public ClientManager()
    {
        _clients = new ConcurrentDictionary<string, ClientInfo>();
        _endPointToPlayerId = new ConcurrentDictionary<string, string>();
    }

    #region 새로운 플레이어 연결 처리
    public ConnectResponseData ConnectPlayer(IPEndPoint endPoint, ConnectData connectData)
    {
        // EndPoint를 문자열 키로 변환
        var endPointKey = endPoint.ToString(); 
        
        // 이미 연결된 클라인지 확인
        if (_endPointToPlayerId.ContainsKey(endPointKey))
        {
            var playerId = _endPointToPlayerId[endPointKey];
            Console.WriteLine($"이미 연결된 클라이언트: {endPointKey} (플레이어ID: {playerId})");

            return new ConnectResponseData
            {
                Success = false,
                PlayerId = playerId,
                Message = "이미 연결된 클라이언트입니다."
            };
        }
        
        // 새로 접속한 플레이어 ID 생성
        string newPlayerId;
        // Lock을 사용해 플레이어 ID 생성
        lock (_playerIdLock)
        {
            newPlayerId = $"Player_{_nextPlayerId++}";
        }
        
        // 새로운 클라이언트 정보 생성
        var clientInfo = new ClientInfo
        {
            PlayerId = newPlayerId,             // 내부적으로 생성한 Player ID
            PlayerName = connectData.PlayerName,// 닉네임
            EndPoint = endPoint,                // 클라이언트 네트워크 엔드포인트
            LastHeartbeat = DateTime.UtcNow,    // 현재 시간을 마지막 하트비트로 설정
            Position = new Vector3(0, 0, 0),    // 초기위치를 원점으로 설정
            Rotation = new Vector3(0, 0, 0),    // 초기회전을 0으로 설정
            // 연결 시간 기록...
        };

        // 생성한 클라이언트를 딕셔너리에 추가
        _clients.TryAdd(newPlayerId, clientInfo);
        _endPointToPlayerId.TryAdd(endPointKey, newPlayerId);
        
        Console.WriteLine($"새 플레이어 연결:{connectData.PlayerName}");
        
        // 연결 성공 응답 메시지 반환
        return new ConnectResponseData()
        {
            Success = true,
            PlayerId = newPlayerId,
            Message = $"연결 성공: {connectData.PlayerName}"
        };
    }
    #endregion

    #region 플레이어 연결 해제

    public ClientInfo? DisconnectPlayer(IPEndPoint endPoint)
    {
        var endPointKey = endPoint.ToString();
        
        // EndPoint로 Player ID 조회  _endPointToPlayerId
        if (_endPointToPlayerId.TryGetValue(endPointKey, out var playerId))
        {
            // Player ID로 Client 정보 삭제
            if (_clients.TryRemove(playerId, out var clientInfo))
            {
                Console.WriteLine($"플레이어 연결해제: {clientInfo.PlayerName} , EndPoint: {endPoint}");
                return clientInfo;
            }
        }
        return null;
    }
    #endregion
    
    // 플레이어 Transform 정보 업데이트
    
    // 플레어어 하트비트 업데이트
    
    // 비활성화 플레이어 제거
    
}
