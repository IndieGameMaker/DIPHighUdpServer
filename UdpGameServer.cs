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

}


