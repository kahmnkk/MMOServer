using System.Net;
using System.Text;
using ServerCore;

class GameSession : Session
{
    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"[OnConnected] {endPoint}");

        var sendBuff = Encoding.UTF8.GetBytes("Hello World!");
        Send(sendBuff);
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        Console.WriteLine($"[OnDisconnected] {endPoint}");
    }

    public override void OnRecv(ArraySegment<byte> buffer)
    {
        var recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
        Console.WriteLine($"[OnRecv] [From Server] {recvData}");
    }

    public override void OnSend(int numOfBytes)
    {
        Console.WriteLine($"[OnSend] [To Server] Transferred Bytes: {numOfBytes}");
    }
}

class Program
{
    public static void Main(string[] args)
    {
        var host = Dns.GetHostName();
        var ipHost = Dns.GetHostEntry(host);
        var ipAddr = ipHost.AddressList[0];
        var endPoint = new IPEndPoint(ipAddr, 8080);

        Connector connector = new();
        connector.Connect(endPoint, () => new GameSession());

        while (true)
        {
        }
    }
}