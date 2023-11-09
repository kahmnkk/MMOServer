namespace ServerCore;

public class RecvBuffer
{
    // [r][][w][][][][][][]
    private readonly ArraySegment<byte> _buffer;
    private int _readPos;
    private int _writePos;

    public RecvBuffer(int bufferSize)
    {
        _buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
    }

    public int DataSize => _writePos - _readPos;
    public int FreeSize => _buffer.Count - _writePos;

    // 데이터를 읽을 범위
    public ArraySegment<byte> ReadSegment => new(_buffer.Array, _buffer.Offset + _readPos, DataSize);

    // 데이터를 쓰는 범위
    public ArraySegment<byte> WriteSegment => new(_buffer.Array, _buffer.Offset + _writePos, FreeSize);

    public void Clean()
    {
        var dataSize = DataSize;
        if (dataSize == 0)
            // rw가 같은 위치
        {
            _readPos = _writePos = 0;
        }
        else
        {
            // 데이터 수신시 완전하지 않은 패킷이 전송되서 남은 데이터가 있을때
            Array.Copy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
            _readPos = 0;
            _writePos = dataSize;
        }
    }

    public bool OnRead(int numOfBytes)
    {
        if (numOfBytes > DataSize)
            return false;

        _readPos += numOfBytes;
        return true;
    }

    public bool OnWrite(int numOfBytes)
    {
        if (numOfBytes > FreeSize)
            return false;

        _writePos += numOfBytes;
        return true;
    }
}