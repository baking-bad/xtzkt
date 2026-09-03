using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Utils;

public interface IRlpElement
{
}

public class RlpItem : IRlpElement
{
    public byte[] Data { get; }

    public RlpItem(byte[] data)
    {
        Data = data;
    }

    public RlpItem(int num)
    {
        Data = num == 0 ? [] : HexNumber.GetBytes(num);
    }

    public RlpItem(string hex)
    {
        Data = Hex.GetBytes(hex);
    }
}

public class RlpList : List<IRlpElement>, IRlpElement
{
}

public class RlpStream(byte[] _bytes)
{
    public static byte[] Encode(IRlpElement e)
    {
        if (e is RlpItem item)
        {
            if (item.Data.Length == 1 && item.Data[0] <= 0x7f)
            {
                return [item.Data[0]];
            }
            else if (item.Data.Length <= 55)
            {
                var lenTag = (byte)(item.Data.Length + 0x80);
                return [lenTag, ..item.Data];
            }
            else
            {
                var lenBytes = HexNumber.GetBytes(item.Data.Length);
                var lenTag = (byte)(lenBytes.Length + 0xb7);
                return [lenTag, ..lenBytes, ..item.Data];
            }
        }
        else if (e is RlpList list)
        {
            byte[] listBytes = [..list.SelectMany(Encode)];
            if (listBytes.Length <= 55)
            {
                var lenTag = (byte)(listBytes.Length + 0xc0);
                return [lenTag, ..listBytes];
            }
            else
            {
                var lenBytes = HexNumber.GetBytes(listBytes.Length);
                var lenTag = (byte)(lenBytes.Length + 0xf7);
                return [lenTag, ..lenBytes, ..listBytes];
            }
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    public bool CanRead => _position < _bytes.Length;

    int _position = 0;

    byte ReadByte()
    {
        return _bytes[_position++];
    }

    int ReadLength(int lengthOfLength)
    {
        if (lengthOfLength > 4 || lengthOfLength == 4 && (_bytes[_position] & 0x80) != 0)
            throw new NotSupportedException("RLP element length is too big");

        int res = _bytes[_position++];
        for (var i = 1; i < lengthOfLength; i++)
            res = (res << 8) | _bytes[_position++];

        return res;
    }

    ReadOnlySpan<byte> ReadBytes(int length)
    {
        var start = _position;
        
        _position += length;
        if (_position > _bytes.Length)
            throw new InvalidOperationException("RLP stream exhausted");

        return _bytes.AsSpan(start, length);
    }

    public IRlpElement Read()
    {
        if (_position >= _bytes.Length)
            throw new InvalidOperationException("RLP stream is empty or exhausted");

        var b = ReadByte();

        // single byte string
        if (b <= 0x7f)
        {
            return new RlpItem([b]);
        }

        // short string (0-55 bytes)
        if (b <= 0xb7)
        {
            var length = b - 0x80;
            return new RlpItem([..ReadBytes(length)]);
        }

        // long string, N+1 bytes for len, then payload
        if (b <= 0xbf)
        {
            var lengthOfLength = b - 0xb7;
            var length = ReadLength(lengthOfLength);
            return new RlpItem([..ReadBytes(length)]);
        }

        // short list (0-55 bytes)
        if (b <= 0xf7)
        {
            var length = b - 0xc0;
            var endPosition = _position + length;
            
            var list = new RlpList();
            while (_position < endPosition)
                list.Add(Read());
            
            return list;
        }

        // long list, N+1 bytes for len, then payload
        else
        {
            var lengthOfLength = b - 0xf7;
            var length = ReadLength(lengthOfLength);
            var endPosition = _position + length;

            var list = new RlpList();
            while (_position < endPosition)
                list.Add(Read());

            return list;
        }
    }
}
