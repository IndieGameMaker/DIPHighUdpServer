using System.Buffers;                   // ArrayPool 
using System.Collections.Concurrent;    // ConcurrentQueue 사용
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HighUDPServer;

// 수신된 데이터 패킷을 저장하는 구조체
public struct ReceivedData
{
    public byte[] Buffer { get; set; }              // 수신 데이터 버퍼
    public int Length { get; set; }                 // 실제로 수신된 데이터 길이
    public IPEndPoint ClientEndPoint { get; set; }  // 수신한 클라이언트 정보
}

// 클라이언트 정보를 저장하는 클래스
public class ClientInfo
{
    public string playerId { get; set; } = string.Empty;    // Player ID
    public IPEndPoint EndPoint { get; set; } = null;        // 클라이언트 EndPoint
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;  // 마지막 하트비트 시간(연결상태 확인용)
    public Vector3 Position { get; set; } = new Vector3();  // 플레이어의 좌표
    public Vector3 Rotation { get; set; } = new Vector3();  // 플레이어의 회전
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
    private readonly Socket _socket;                // UDP 통신을 위한 소켓 객체
    private readonly IPEndPoint _serverEndPoint;    // 서버 바인딩 주소, 포트
    
    // 메모리 풀을 사용해서 버퍼 재사용
    private readonly ArrayPool<byte> _bufferPool;
    
    // 수신 패킷을 저장하는 큐(스레드 세이프)
    private readonly ConcurrentQueue<ReceivedData> _receiveQueue;
    
    // TODO: 접속한 클라이언트 정보를 저장하는 딕셔너리
    private readonly ConcurrentDictionary<string, ClientInfo> _clients;
    
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
        _clients = new ConcurrentDictionary<string, ClientInfo>();
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
                        ClientEndPoint = (IPEndPoint) result.RemoteEndPoint,
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
                var message = Encoding.UTF8.GetString(receivedData.Buffer, 0, receivedData.Length);
                Console.WriteLine($"수신된 메시지: {message}");
                
                // 간단한 에코 처리
                var response = $"ECHO: {message}";
                var responseBytes = Encoding.UTF8.GetBytes(response);

                await _socket.SendToAsync(new ArraySegment<byte>(responseBytes, 0, responseBytes.Length), SocketFlags.None, receivedData.ClientEndPoint);
                
                _bufferPool.Return(receivedData.Buffer);
            }
        }
    }

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


