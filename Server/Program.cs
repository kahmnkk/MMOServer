using System.Net;
using System.Text;
using ServerCore;

namespace Server;

class GameSession : Session
{
    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"[OnConnected] {endPoint}");

        var sendBuff = Encoding.UTF8.GetBytes("Welcome to Server!");
        Send(sendBuff);
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        Console.WriteLine($"[OnDisconnected] {endPoint}");
    }

    public override int OnRecv(ArraySegment<byte> buffer)
    {
        string recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
        Console.WriteLine($"[OnRecv] [From Client] {recvData}");
        return buffer.Count;
    }

    public override void OnSend(int numOfBytes)
    {
        Console.WriteLine($"[OnSend] [To Client] Transferred Bytes: {numOfBytes}");
    }
}

class Program
{
    private static readonly Listener _listener = new(); 
    
    public static void Main(string[] args)
    {
        var host = Dns.GetHostName();
        var ipHost = Dns.GetHostEntry(host);
        var ipAddr = ipHost.AddressList[0];
        var endPoint = new IPEndPoint(ipAddr, 8080);

        _listener.Init(endPoint, () => new GameSession());
        Console.WriteLine("Listening...");

        // 게임서버를 계속 켜놓기 위함
        while (true)
        {
        }
    }
}
