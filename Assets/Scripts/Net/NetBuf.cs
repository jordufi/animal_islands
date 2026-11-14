using System;
using System.Text;
using UnityEngine;

// Online messages: [byte NetMsg][payload]. NetBuf writes a payload, NetRead reads it back. Our own tiny
// serializer (the bytes are copied into / out of Netcode's buffers when sent / received), so the game code
// never deals with Netcode types.
public class NetBuf
{
    byte[] data = new byte[64];
    int length;

    public byte[] Data => data;
    public int Length => length;

    public static NetBuf Make(NetMsg op)
    {
        var b = new NetBuf();
        b.Byte((byte)op);
        return b;
    }

    // An already complete message (the host relays what a client sent).
    public static NetBuf Wrap(byte[] raw) => new NetBuf { data = raw, length = raw.Length };

    // position packed in 3 shorts (centimetres, ±327 m)
    public NetBuf Pos(Vector3 p) => Short(Mathf.RoundToInt(p.x * 100f)).Short(Mathf.RoundToInt(p.y * 100f)).Short(Mathf.RoundToInt(p.z * 100f));

    void Ensure(int extra)
    {
        if (length + extra <= data.Length) return;
        int size = data.Length * 2;
        while (size < length + extra) size *= 2;
        Array.Resize(ref data, size);
    }

    public NetBuf Byte(byte v) { Ensure(1); data[length++] = v; return this; }
    public NetBuf SByte(int v) => Byte((byte)(sbyte)Mathf.Clamp(v, -128, 127));
    public NetBuf Bool(bool v) => Byte(v ? (byte)1 : (byte)0);
    public NetBuf UShort(int v) { Ensure(2); data[length++] = (byte)v; data[length++] = (byte)(v >> 8); return this; }
    public NetBuf Short(int v) => UShort((ushort)(short)Mathf.Clamp(v, short.MinValue, short.MaxValue));
    public NetBuf Int(int v) { Ensure(4); for (int i = 0; i < 4; i++) data[length++] = (byte)(v >> (8 * i)); return this; }
    public NetBuf Float(float v) => Int(BitConverter.SingleToInt32Bits(v));
    public NetBuf Vec(Vector3 v) => Float(v.x).Float(v.y).Float(v.z);
    // angle in degrees packed in 2 bytes
    public NetBuf Angle(float deg) => UShort(Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 360f * 65535f));
    // 0..1 packed in one byte
    public NetBuf Unit(float v) => Byte((byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f));

    public NetBuf Str(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s ?? "");
        UShort(bytes.Length);
        Ensure(bytes.Length);
        Array.Copy(bytes, 0, data, length, bytes.Length);
        length += bytes.Length;
        return this;
    }
}

public class NetRead
{
    readonly byte[] data;
    int pos;

    public NetRead(byte[] data, int start) { this.data = data; pos = start; }

    public bool More => pos < data.Length;
    public byte[] Raw => data;   // the whole message, op included (for relaying)
    public Vector3 Pos() => new Vector3(Short() / 100f, Short() / 100f, Short() / 100f);

    public byte Byte() => pos < data.Length ? data[pos++] : (byte)0;
    public int SByte() => (sbyte)Byte();
    public bool Bool() => Byte() != 0;
    public int UShort() { int v = Byte(); v |= Byte() << 8; return v; }
    public int Short() => (short)UShort();
    public int Int() { int v = 0; for (int i = 0; i < 4; i++) v |= Byte() << (8 * i); return v; }
    public float Float() => BitConverter.Int32BitsToSingle(Int());
    public Vector3 Vec() => new Vector3(Float(), Float(), Float());
    public float Angle() => UShort() / 65535f * 360f;
    public float Unit() => Byte() / 255f;

    public string Str()
    {
        int n = UShort();
        if (n <= 0 || pos + n > data.Length) { pos = Mathf.Min(data.Length, pos + Mathf.Max(0, n)); return ""; }
        string s = Encoding.UTF8.GetString(data, pos, n);
        pos += n;
        return s;
    }
}
