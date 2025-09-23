using System.Net;               // IPEndPoint, IPAddress 사용을 위한 네임스페이스
using System.Net.Sockets;       // UdpClient 사용을 위한 네임스페이스
using HighUDPServer.Protocal;   // 게임 프로토콜 사용을 위한 네임스페이스

namespace HighUDPServer; 

// UDP 게임 서버 테스트용 클라이언트
public class TestClient
{
    private readonly UdpClient _udpClient;          // UDP 클라이언트
    private readonly IPEndPoint _serverEndPoint;    // 서버 엔드포인트
    private string? _playerId;                      // 할당받은 플레이어 ID
    private readonly string _playerName;            // 플레이어 이름
    
    // 테스트 클라이언트 생성자
    public TestClient(string serverHost, int serverPort, string playerName)
    {
        _udpClient = new UdpClient();                                               // UDP 클라이언트 생성
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(serverHost), serverPort);  // 서버 엔드포인트 설정
        _playerName = playerName;                                                   // 플레이어 이름 설정
    }

    public async Task EchoAsync()
    {
        Console.WriteLine($"{_playerName} PING Test");
        
        // 에코 메시지
        var echo = GameProtocal.CreateMessage(MessageType.Echo, "CLIENT", "PING");
        await SendMessageAsync(echo);

        var response = await ReceiveResponseAsync();
        if (response.Type == MessageType.Echo)
        {
            // 응답 데이터 역직렬화
            var responseData = GameProtocal.GetData<string>(response);
            Console.WriteLine($"{responseData}");
        }
    }
    
    // 서버에 연결 요청
    public async Task ConnectAsync()
    {
        Console.WriteLine($"[{_playerName}] 서버에 연결 시도 중...");

        // 연결 요청 메시지 생성
        var connectData = new ConnectData
        {
            PlayerName = _playerName,       // 플레이어 이름
        };

        // 연결 메시지 생성
        var connectMessage = GameProtocal.CreateMessage(MessageType.Connect, "", connectData);
        // 연결 메시지 전송
        await SendMessageAsync(connectMessage); 

        // 응답 대기 (간단한 구현)
        var responseTask = ReceiveResponseAsync(); // 응답 수신 작업
        // 먼저 완료되는 작업 대기
        var completedTask = await Task.WhenAny(responseTask); 
        

        // 응답 메시지 획득
        var response = await responseTask; 
        
        // 연결 응답인 경우
        if (response.Type == MessageType.ConnectResponse) 
        {
            // 응답 데이터 역직렬화
            var responseData = GameProtocal.GetData<ConnectResponseData>(response); 
            
            if (responseData.Success)
            {
                _playerId = responseData.PlayerId; 
                
                Console.WriteLine($"[{_playerName}] 연결 성공! 플레이어 ID: {_playerId}"); // 연결 성공 로그
                Console.WriteLine($"[{_playerName}] 서버 메시지: {responseData.Message}"); // 서버 메시지 출력
            }
            else
            {
                Console.WriteLine($"[{_playerName}] 연결 실패: {responseData.Message}"); // 연결 실패 로그
            }
        }
    }

    // 연결 해제
    public async Task DisconnectAsync()
    {
        Console.WriteLine($"[{_playerName}] 연결 해제 요청");

        // 연결 해제 메시지 생성
        var message = GameProtocal.CreateMessage(MessageType.Disconnect, _playerId, "연결 해제 요청");
        await SendMessageAsync(message);

        _udpClient.Close();     // UDP 클라이언트 닫기
    }

    // 서버에 메시지 전송
    private async Task SendMessageAsync(GameMessage message)
    {
        var messageBytes = GameProtocal.Serialize(message); // 메시지를 바이트 배열로 직렬화
        await _udpClient.SendAsync(messageBytes, messageBytes.Length, _serverEndPoint); // UDP로 메시지 전송
    }

    /// <summary>
    /// 서버로부터 응답 수신
    /// </summary>
    private async Task<GameMessage> ReceiveResponseAsync()
    {
        var result = await _udpClient.ReceiveAsync(); // UDP 메시지 수신
        return GameProtocal.Deserialize(result.Buffer, result.Buffer.Length); // 메시지 역직렬화하여 반환
    }
}

// 테스트 클라이언트 실행을 위한 프로그램
public class TestClientProgram
{
    // 테스트 클라이언트 메인 메서드
    public static async Task RunTestClientsAsync()
    {
        // 테스트 시작 로그
        Console.WriteLine("=== UDP 게임 서버 테스트 클라이언트 ==="); 

        // 테스트 클라이언트 목록
        var clients = new List<TestClient>(); 

        // 3개의 테스트 클라이언트 생성
        for (int i = 1; i <= 3; i++)
        {
            // 테스트 클라이언트 생성
            var client = new TestClient("127.0.0.1", 9999, $"TestPlayer{i}");
            // 클라이언트 목록에 추가
            clients.Add(client); 
        }

        try
        {
            // 모든 클라이언트 연결
            foreach (var client in clients)
            {
                await client.ConnectAsync();    // 클라이언트 연결
                await Task.Delay(500);          // 500ms 간격으로 연결
            }

            // 에코 메시지
            foreach (var client in clients)
            {
                await client.EchoAsync();
                await Task.Delay(1000);
            }
            
            // 모든 클라이언트 연결 해제
            foreach (var client in clients)
            {
                await client.DisconnectAsync(); // 클라이언트 연결 해제
            }

            Console.WriteLine("테스트 완료!"); // 테스트 완료 로그
        }
        catch (Exception ex)
        {
            Console.WriteLine($"테스트 중 오류 발생: {ex.Message}"); // 테스트 오류 로그
        }
    }
}