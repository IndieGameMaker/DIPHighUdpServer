using System.Buffers;                   // ArrayPool 
using System.Collections.Concurrent;    // ConcurrentQueue 사용
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HighUDPServer;

public class UdpGameServer
{
    private readonly Socket _socket;                // UDP 통신을 위한 소켓 객체
    private readonly IPEndPoint _serverEndPoint;    // 서버 바인딩 주소, 포트
    
    // 메모리 풀을 사용해서 버퍼 재사용
    private readonly ArrayPool<byte> _bufferPool;
    
    // TODO: 수신 패킷을 저장하는 큐(스레드 세이프)
    // private readonly ConcurrentQueue<> _receiveQueue;
    
    // TODO: 접속한 클라이언트 정보를 저장하는 딕셔너리
    
    // 취소 토큰
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    // 서버 실행여부
    private bool _isRunning;
    
    // 수신 버퍼 사이즈 (1KB)
    private readonly int _bufferSize = 1024;
    
    // 서버 포트 번호
    private readonly int _port;
}
