using System.Buffers; // ArrayPool 
using System.Collections.Concurrent; // ConcurrentQueue 사용
using System.Net;
using System.Net.Sockets;
using System.Text;
using HighUDPServer.Protocal;

namespace HighUDPServer;

// 수신된 데이터 패킷을 저장하는 구조체
public struct ReceivedData
{
    public byte[] Buffer { get; set; } // 수신 데이터 버퍼
    public int Length { get; set; } // 실제로 수신된 데이터 길이
    public IPEndPoint ClientEndPoint { get; set; } // 수신한 클라이언트 정보
}

// 클라이언트 정보를 저장하는 클래스
public class ClientInfo
{
    public string PlayerId { get; set; } = string.Empty; // Player ID
    public string PlayerName { get; set; } = string.Empty; // 플레이어 닉네임
    public IPEndPoint EndPoint { get; set; } = null; // 클라이언트 EndPoint
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow; // 마지막 하트비트 시간(연결상태 확인용)
    public Vector3 Position { get; set; } = new Vector3(); // 플레이어의 좌표
    public Vector3 Rotation { get; set; } = new Vector3(); // 플레이어의 회전
}

// 3D 벡터 데이터
public struct Vector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3(float x = 0, float y = 0, float z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }
}

public class UdpGameServer
{
    private readonly Socket _socket; // UDP 통신을 위한 소켓 객체
    private readonly IPEndPoint _serverEndPoint; // 서버 바인딩 주소, 포트

    // 메모리 풀을 사용해서 버퍼 재사용
    private readonly ArrayPool<byte> _bufferPool;

    // 수신 패킷을 저장하는 큐(스레드 세이프)
    private readonly ConcurrentQueue<ReceivedData> _receiveQueue;

    // 접속한 클라이언트 정보를 저장하는 딕셔너리
    // private readonly ConcurrentDictionary<string, ClientInfo> _clients;

    // 클라이언트 연결 관리
    private readonly ClientManager _clientManager;

    // 취소 토큰
    private readonly CancellationTokenSource _cancellationTokenSource;

    // 서버 실행여부
    private bool _isRunning;

    // 수신 버퍼 사이즈 (1KB)
    private readonly int _bufferSize = 1024;

    // 서버 포트 번호
    private readonly int _port;

    // 생성자
    public UdpGameServer(int port)
    {
        _port = port;
        // 소켓 생성 (IPv4, UDP 소켓)
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverEndPoint = new IPEndPoint(IPAddress.Any, port);

        // 버퍼 설정 (공유 메모리 풀)
        _bufferPool = ArrayPool<byte>.Shared;
        // 스레드 세이프한 수신 Queue 초기화
        _receiveQueue = new ConcurrentQueue<ReceivedData>();

        // 클라이언트 딕셔너리 초기화
        // _clients = new ConcurrentDictionary<string, ClientInfo>();

        // 클라이언트 관리 클래스 초기화
        _clientManager = new ClientManager();

        // 취소 토큰 소스 초기화
        _cancellationTokenSource = new CancellationTokenSource();
    }

    // 서버 시작 메소드
    public async Task StartAsync()
    {
        try
        {
            // 소켓 바인딩
            _socket.Bind(_serverEndPoint);
            // 서버 실행 상태로 변경
            _isRunning = true;

            Console.WriteLine($"UDP 게임 서버가 포트{_port}에서 시작되었습니다.");

            // 멀티스레드로 데이터 수신 및 처리
            // 수신 로직 (스레드 동작)
            var receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token));
            // 메시지 처리로직
            var processTask = Task.Run(() => ProcessLoopAsync(_cancellationTokenSource.Token));


            // 작업이 완료될 때 까지 대기
            await Task.WhenAll(receiveTask, processTask);
        }
        catch (Exception e)
        {
            Console.WriteLine($"서버 시작중 오류 발생 {e.Message}");
            throw;
        }
    }

    // 데이터 수신 루프 (별도의 스레드에서 실행)
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 메모리 풀에서 사용할 버퍼를 대여
                var buffer = _bufferPool.Rent(_bufferSize);

                // 클라이언트 엔드포인 저장할 변수
                var endPoint = new IPEndPoint(IPAddress.Any, 0);
                var result = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, endPoint);

                // 데이터를 수신한 경우
                if (result.ReceivedBytes > 0)
                {
                    Console.WriteLine("메시지 수신" + Encoding.UTF8.GetString(buffer, 0, result.ReceivedBytes));
                    // 수신된 데이터를 큐에 추가
                    _receiveQueue.Enqueue(new ReceivedData
                    {
                        Buffer = buffer,
                        Length = result.ReceivedBytes,
                        ClientEndPoint = (IPEndPoint)result.RemoteEndPoint,
                    });
                }
                else
                {
                    // 사용하지 않는 버퍼는 반환
                    _bufferPool.Return(buffer);
                    await Task.Delay(1, cancellationToken);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"데이터 수신중 오류 발생: {e.Message}");
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    // 수신 데이터 처리 메서드
    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            if (_receiveQueue.TryDequeue(out var receivedData)) // 큐에서 데이터 추출
            {
                // var message = Encoding.UTF8.GetString(receivedData.Buffer, 0, receivedData.Length);
                // Console.WriteLine($"수신된 메시지: {message}");
                //
                // // 간단한 에코 처리
                // var response = $"ECHO: {message}";
                // var responseBytes = Encoding.UTF8.GetBytes(response);

                // await _socket.SendToAsync(new ArraySegment<byte>(responseBytes, 0, responseBytes.Length), SocketFlags.None, receivedData.ClientEndPoint);

                try
                {
                    await ProcessReceivedDataAsync(receivedData);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    _bufferPool.Return(receivedData.Buffer);
                }
            }
        }
    }

    // 수신된 데이터 처리 메서드
    private async Task ProcessReceivedDataAsync(ReceivedData data)
    {
        try
        {
            // 수신된 데이터를 게임 메시지로 역직렬화 처리
            var gameMessage = GameProtocal.Deserialize(data.Buffer, data.Length);
            Console.WriteLine($"수신 메시지: {gameMessage.Type} | EndPoint: {data.ClientEndPoint} | 플레이어ID: {gameMessage.PlayerId}");
            
            // 메시지 타입에 따라 분기
            switch (gameMessage.Type)
            {
                case MessageType.Connect:
                    await HandleConnectMessage(gameMessage, data.ClientEndPoint);
                    break;
                case MessageType.Disconnect:
                    await HandleDisconnectMessage(gameMessage, data.ClientEndPoint);
                    break;
                case MessageType.PlayerJoin:
                    break;
                case MessageType.TransformUpdate:
                    break;
                case MessageType.TransformSync:
                    break;
                case MessageType.Heartbeat:
                    break;
                case MessageType.Echo:
                    await HandleEchoMessage(gameMessage, data.ClientEndPoint);
                    break;
                default:
                    Console.WriteLine("알 수 없는 메시지 타입");
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"메시지 처리중 오류:{e.Message}");
        }
    }



    #region 메시지 타입별 전송 메서드
    
    // 연결 요청 메시지 처리
    private async Task HandleConnectMessage(GameMessage message, IPEndPoint clientEndPoint)
    {
        // 연결요청 데이터 역직렬화
        var connectData = GameProtocal.GetData<ConnectData>(message);
        // 클라이언트 관리자에 연결 요청
        var response = _clientManager.ConnectPlayer(clientEndPoint, connectData);
        
        // 연결 응답 메시지 생성
        var responseMessage = GameProtocal.CreateMessage(MessageType.ConnectResponse, response.PlayerId, response);
        // 응답 메시지 전송
        await SendGameMessage(responseMessage, clientEndPoint);
    }
    
    private async Task HandleDisconnectMessage(GameMessage message, IPEndPoint clientEndPoint)
    {
        // 클라이언트 관리자를 통한 연결 해제
        var disconnectedClient = _clientManager.DisconnectPlayer(clientEndPoint); 
        
        if (disconnectedClient != null)
        {
            // 플레이어 퇴장 브로드캐스트
            await BroadcastPlayerLeave(disconnectedClient.PlayerId, disconnectedClient.PlayerName); 
        }
    }    
    
    #region 송신 처리 메서드

    /// <summary>
    /// 플레이어 퇴장 브로드캐스트
    /// </summary>
    private async Task BroadcastPlayerLeave(string playerId, string playerName)
    {
        var playerData = new PlayerData
        {
            PlayerId = playerId, // 플레이어 ID
            PlayerName = playerName, // 플레이어 이름
            Position = new Vector3(), // 빈 위치
            Rotation = new Vector3(), // 빈 회전
            LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() // 현재 시간
        };
        
        var message = GameProtocal.CreateMessage(MessageType.PlayerLeave, "SERVER", playerData); // 플레이어 퇴장 메시지 생성
        await BroadcastToAll(message, playerId); // 해당 플레이어를 제외한 모든 클라이언트에게 브로드캐스트
    }
    
    // 모든 클라이언트에게 메시지 브로드캐스트 (특정 플레이어 제외)
    /*
      1. 순차적 방식 (await foreach)

       foreach (var client in _clientManager.ConnectedPlayers)
       {
           if (excludePlayerId != null && client.PlayerId == excludePlayerId)
               continue;

           await SendGameMessage(message, client.EndPoint); // 순차 실행
       }

       2. 병렬 방식 (Task.Add + Task.WhenAll)

       var tasks = new List<Task>();
       foreach (var client in _clientManager.ConnectedPlayers)
       {
           if (excludePlayerId != null && client.PlayerId == excludePlayerId)
               continue;

           tasks.Add(SendGameMessage(message, client.EndPoint)); // 병렬 준비
       }
       await Task.WhenAll(tasks); // 모든 작업 동시 실행

       성능 차이:

       순차적 방식

       클라이언트 A: [████████] 100ms
       클라이언트 B:          [████████] 100ms
       클라이언트 C:                   [████████] 100ms
       총 소요 시간: 300ms

       병렬 방식 (현재 코드)

       클라이언트 A: [████████] 100ms
       클라이언트 B: [████████] 100ms (동시 실행)
       클라이언트 C: [████████] 100ms (동시 실행)
       총 소요 시간: 100ms

       장단점 비교:

       | 구분      | 순차적 방식        | 병렬 방식         |
       |---------|---------------|---------------|
       | 성능      | 느림 (N × 전송시간) | 빠름 (최대 전송시간)  |
       | 메모리     | 적음            | 많음 (Task 객체들) |
       | 에러 처리   | 즉시 중단 가능      | 일부 실패해도 계속    |
       | 네트워크 부하 | 분산됨           | 집중됨           |
       | 코드 복잡도  | 단순            | 약간 복잡         |

       실제 성능 시나리오:

       100명 접속, 각 전송 50ms인 경우:
       - 순차적: 100 × 50ms = 5초
       - 병렬: 50ms = 0.05초 (100배 빠름)

       권장사항:

       현재 코드(병렬)가 올바른 선택:
       // 게임에서 브로드캐스트는 실시간성이 중요
       // 예: 플레이어 위치 동기화, 채팅 메시지

       순차적이 나은 경우:
       // 순서가 중요하거나 네트워크 대역폭 제한이 있는 경우
       // 예: 대용량 파일 전송, 순차적 명령어 실행

       결론: 게임 서버의 브로드캐스트에서는 병렬 방식이 필수입니다. 실시간
       멀티플레이어 게임에서 지연은 치명적이기 때문입니다.
     */    
    private async Task BroadcastToAll(GameMessage message, string? excludePlayerId = null)
    {
        var tasks = new List<Task>(); // 비동기 전송 작업 목록

        // 클라이언트에게 응답 전송
        // 모든 접속 중인 플레이어 순회
        foreach (var client in _clientManager.ConnectedPlayers) 
        {
            // 제외할 플레이어는 건너뛰기
            if (excludePlayerId != null && client.PlayerId == excludePlayerId) 
                continue;
                
            // 각 클라이언트에게 메시지 전송 작업 추가
            tasks.Add(SendGameMessage(message, client.EndPoint)); 
        }
        
        await Task.WhenAll(tasks); // 모든 전송 작업이 완료될 때까지 대기
    }
    
    // 에코 메시지 처리
    private async Task HandleEchoMessage(GameMessage message, IPEndPoint clientEndPoint)
    {
        // 에코 응답 메시지 생성
        var echoResponce = GameProtocal.CreateMessage(MessageType.Echo, "SERVER", $"ECHO:{message.Data}");
        await SendGameMessage(echoResponce, clientEndPoint);
    }
    #endregion

    // 게임 메시지 전송 공통 메소드
    private async Task SendGameMessage(GameMessage message, IPEndPoint clientEndPoint)
    {
        var messageData = GameProtocal.Serialize(message);
        await _socket.SendToAsync(new ArraySegment<byte>(messageData,0,messageData.Length), clientEndPoint);
        
        // var responseBytes = Encoding.UTF8.GetBytes(response);
        // await _socket.SendToAsync(new ArraySegment<byte>(responseBytes, 0, responseBytes.Length), SocketFlags.None, receivedData.ClientEndPoint);
    }
    #endregion

    public async Task StopAsync()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();

        await Task.Delay(100);

        // 소켓 종료
        _socket.Close();
        _socket.Dispose();

        Console.WriteLine("UDP 게임 서버 종료");
    }
}