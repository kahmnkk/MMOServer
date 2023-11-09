using System.Net;
using System.Net.Sockets;

namespace ServerCore;

public abstract class Session
{
    private Socket _socket;
    private int _disconnected;

    private readonly RecvBuffer _recvBuffer = new(1024);

    private readonly object _lock = new();
    private readonly Queue<byte[]> _sendQueue = new();
    private readonly List<ArraySegment<byte>> _pendingList = new();
    private readonly SocketAsyncEventArgs _sendArgs = new();
    private readonly SocketAsyncEventArgs _recvArgs = new();

    public abstract void OnConnected(EndPoint endPoint);
    public abstract void OnDisconnected(EndPoint endPoint);
    public abstract int OnRecv(ArraySegment<byte> buffer);
    public abstract void OnSend(int numOfBytes);

    public void Start(Socket socket)
    {
        _socket = socket;

        _recvArgs.Completed += OnRecvComplete;
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
            byte[] buff = _sendQueue.Dequeue();
            _pendingList.Add(new ArraySegment<byte>(buff, 0, buff.Length));
        }

        _sendArgs.BufferList = _pendingList;

        bool pending = _socket.SendAsync(_sendArgs);
        if (pending == false)
            OnSendComplete(null, _sendArgs);
    }

    private void OnSendComplete(object? sender, SocketAsyncEventArgs args)
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
        _recvBuffer.Clean();
        ArraySegment<byte> segment = _recvBuffer.WriteSegment;
        _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count); // 이만큼이 _recvBuffer의 남은 공간이라고 명시

        bool pending = _socket.ReceiveAsync(_recvArgs);
        if (pending == false)
            OnRecvComplete(null, _recvArgs);
    }

    private void OnRecvComplete(object? sender, SocketAsyncEventArgs args)
    {
        if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            // TODO 비정상 유저 체크 후 disconnect
            try
            {
                // write 커서 이동
                if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    throw new Exception("error");

                // 패킷을 받았을 때 얼마나 처리했는지
                // 온전한 패킷이 아닐 경우 데이터를 모두 처리하지 못한다
                int procession = OnRecv(_recvBuffer.ReadSegment);
                if (procession < 0 || _recvBuffer.DataSize < procession)
                    throw new Exception("error");

                // read 커서 이동
                if (_recvBuffer.OnRead(procession) == false)
                    throw new Exception("error");

                // 완료 후 재등록
                RegisterRecv();
            }
            catch (Exception e)
            {
                Disconnect();
                
                Console.WriteLine($"[OnRecvComplete] Fail: {e}");
            }
        else
            Disconnect();
    }

    #endregion
}