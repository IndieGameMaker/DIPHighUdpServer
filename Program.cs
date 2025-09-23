namespace HighUDPServer;

class Program
{
    static async Task Main(string[] args)
    {
        // 테스트 클라이언트 실행여부 확인
        if (args.Length > 0 && args[0] == "test")
        {
            await TestClientProgram.RunTestClientsAsync();
            return;
        }
        
        Console.WriteLine("=== UDP 게임서버 시작===");

        const int port = 9999;

        // UDP 게임 서버 인스턴스 생성
        var server = new UdpGameServer(port);

        // Ctrl + C 이벤트 연결
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("서버 종료 중 ...");
            
            // 서버 종료 작업을 처리
            // StopServer 처리
            await server.StopAsync();
            Environment.Exit(0);
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