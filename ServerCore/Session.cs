using System.Net;
using System.Net.Sockets;

namespace ServerCore;

public abstract class Session
{
    private Socket _socket;
    private int _disconnected;

    private readonly object _lock = new();
    private readonly Queue<byte[]> _sendQueue = new();
    private readonly List<ArraySegment<byte>> _pendingList = new();
    private readonly SocketAsyncEventArgs _sendArgs = new();
    private readonly SocketAsyncEventArgs _recvArgs = new();

    public abstract void OnConnected(EndPoint endPoint);
    public abstract void OnDisconnected(EndPoint endPoint);
    public abstract void OnRecv(ArraySegment<byte> buffer);
    public abstract void OnSend(int numOfBytes);

    public void Start(Socket socket)
    {
        _socket = socket;

        _recvArgs.Completed += OnRecvComplete;
        _recvArgs.SetBuffer(new byte[1024], 0, 1024);

        _sendArgs.Completed += OnSendComplete; // 이벤트 핸들러 등록

        RegisterRecv();
    }

    public void Send(byte[] sendBuff)
    {
        lock (_lock)
        {
            _sendQueue.Enqueue(sendBuff);
            if (_pendingList.Count == 0)
                RegisterSend();
        }
    }

    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _disconnected, 1) == 1)
            return;

        OnDisconnected(_socket.RemoteEndPoint);

        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
    }

    # region 네트워크 통신

    private void RegisterSend()
    {
        // 한번에 큐에 있는 패킷을 모두 전송
        while (_sendQueue.Count > 0)
        {
            var buff = _sendQueue.Dequeue();
            _pendingList.Add(new ArraySegment<byte>(buff, 0, buff.Length));
        }

        _sendArgs.BufferList = _pendingList;

        var pending = _socket.SendAsync(_sendArgs);
        if (pending == false)
            OnSendComplete(null, _sendArgs);
    }

    private void OnSendComplete(object sender, SocketAsyncEventArgs args)
    {
        lock (_lock)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                try
                {
                    // 대기중인 목록을 모두 전송함 -> 목록 클리어해주기 
                    _sendArgs.BufferList = null;
                    _pendingList.Clear();

                    OnSend(_sendArgs.BytesTransferred);

                    if (_sendQueue.Count > 0)
                        // 남은 sendQueue 가 있으면 처리
                        RegisterSend();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[OnSendComplete] Fail: {e}");
                }
            else
                Disconnect();
        }
    }

    private void RegisterRecv()
    {
        var pending = _socket.ReceiveAsync(_recvArgs);
        if (pending == false)
            OnRecvComplete(null, _recvArgs);
    }

    private void OnRecvComplete(object sender, SocketAsyncEventArgs args)
    {
        if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            // TODO 비정상 유저 체크 후 disconnect
            try
            {
                OnRecv(new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred));

                // 완료 후 재등록
                RegisterRecv();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[OnRecvComplete] Fail: {e}");
            }
        else
            Disconnect();
    }

    #endregion
}