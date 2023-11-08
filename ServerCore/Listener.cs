using System.Net;
using System.Net.Sockets;

namespace ServerCore;

public class Listener
{
    private Socket _listenSocket;
    private Func<Session> _sessionFactory;

    public void Init(IPEndPoint endPoint, Func<Session> onAcceptHandler)
    {
        _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _sessionFactory += onAcceptHandler;

        _listenSocket.Bind(endPoint);

        // 최대 대기수: 10
        _listenSocket.Listen(10);

        for (var i = 0; i < 10; i++)
        {
            var args = new SocketAsyncEventArgs();
            args.Completed += OnAcceptComplete; // 실제 요청이 오면 이벤트 발생 - OnAcceptComplete 호출 (별도의 쓰레드에서 동작)
            RegisterAccept(args); // 클라 등록 대기중...
        }
    }

    private void RegisterAccept(SocketAsyncEventArgs args)
    {
        // 기존 데이터 초기화
        args.AcceptSocket = null;

        var pending = _listenSocket.AcceptAsync(args); // 예약만 진행
        if (pending == false) // 대기중인게 있을 경우 바로 진행
            OnAcceptComplete(null, args);
    }

    private void OnAcceptComplete(object sender, SocketAsyncEventArgs args)
    {
        // 멀티 쓰레드로 실행될 수 있음을 염두
        if (args.SocketError == SocketError.Success)
        {
            // 유저가 연결 됐을때
            var session = _sessionFactory.Invoke();
            session.Start(args.AcceptSocket);
            session.OnDisconnected(args.AcceptSocket.RemoteEndPoint);
        }
        else
        {
            Console.WriteLine($"[OnAcceptComplete] Fail: {args.SocketError.ToString()}");
        }

        // 수락 완료 후 다시 예약 받을 수 있도록
        RegisterAccept(args);
    }
}