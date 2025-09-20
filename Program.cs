namespace HighUDPServer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== UDP 게임서버 시작===");

        const int port = 9999;

        // UDP 게임 서버 인스턴스 생성
        var server = new UdpGameServer(port);

        // Ctrl + C 이벤트 연결
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("서버 종료 중 ...");
            
            // 서버 종료 작업을 처리
            // TODO : StopServer 처리
        };        
        
        try
        {
            await server.StartAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"서버 실행 중 오류 발생: {e.Message}");
        }
        
        

    }
}