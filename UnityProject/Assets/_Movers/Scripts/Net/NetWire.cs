using System;
using System.Text;
using UnityEngine;

namespace Movers
{
    // The Movers-owned byte format (NETCODE_SLICE 2, 12). Little endian, no NGO types, safe code
    // only: a bad hook can lose a record, never corrupt memory. Quantisation:
    //   half      IEEE 754 binary16, 2 bytes
    //   unit      0..1 clamped, 1 byte (1/255)
    //   angle     0..360 degrees, 2 bytes (0.0055 degrees)
    //   rotation  smallest three, 4 bytes: 2 bits for the dropped component, 3 x 10 bits
    //   string    u8 byte count + UTF-8, at most 200 bytes, 255 = null
    public sealed class NetWriter
    {
        public const int MaxStringBytes = 200;

        byte[] buf;
        int length;

        public NetWriter(int capacity = 256) { buf = new byte[Mathf.Max(16, capacity)]; }

        public int Length => length;
        internal byte[] Buffer => buf;

        internal void Clear() { length = 0; }
        internal void Truncate(int newLength) { if (newLength >= 0 && newLength < length) length = newLength; }

        internal void PatchUShort(int at, ushort v)
        {
            buf[at] = (byte)v;
            buf[at + 1] = (byte)(v >> 8);
        }

        internal void Append(byte[] src, int start, int count)
        {
            Ensure(count);
            System.Buffer.BlockCopy(src, start, buf, length, count);
            length += count;
        }

        void Ensure(int more)
        {
            if (length + more <= buf.Length) return;
            int size = buf.Length * 2;
            while (size < length + more) size *= 2;
            Array.Resize(ref buf, size);
        }

        public void WriteByte(byte v) { Ensure(1); buf[length++] = v; }
        public void WriteSByte(sbyte v) { WriteByte((byte)v); }
        public void WriteBool(bool v) { WriteByte(v ? (byte)1 : (byte)0); }

        public void WriteUShort(ushort v)
        {
            Ensure(2);
            buf[length++] = (byte)v;
            buf[length++] = (byte)(v >> 8);
        }

        public void WriteShort(short v) { WriteUShort((ushort)v); }

        public void WriteUInt(uint v)
        {
            Ensure(4);
            buf[length++] = (byte)v;
            buf[length++] = (byte)(v >> 8);
            buf[length++] = (byte)(v >> 16);
            buf[length++] = (byte)(v >> 24);
        }

        public void WriteInt(int v) { WriteUInt((uint)v); }

        public void WriteULong(ulong v)
        {
            WriteUInt((uint)v);
            WriteUInt((uint)(v >> 32));
        }

        public void WriteFloat(float v) { WriteUInt(NetBits.FloatToUInt(v)); }
        public void WriteHalf(float v) { WriteUShort(Mathf.FloatToHalf(v)); }
        public void WriteUnit(float v01) { WriteByte((byte)Mathf.RoundToInt(Mathf.Clamp01(v01) * 255f)); }

        public void WriteAngle(float degrees)
        {
            float d = Mathf.Repeat(degrees, 360f);
            WriteUShort((ushort)(Mathf.RoundToInt(d / 360f * 65536f) & 0xFFFF));
        }

        public void WriteVector3(Vector3 v) { WriteFloat(v.x); WriteFloat(v.y); WriteFloat(v.z); }
        public void WriteVector3Half(Vector3 v) { WriteHalf(v.x); WriteHalf(v.y); WriteHalf(v.z); }

        public void WriteRotation(Quaternion q) { WriteUInt(NetBits.PackRotation(q)); }

        public void WriteString(string s)
        {
            if (s == null) { WriteByte(255); return; }
            int count = Encoding.UTF8.GetByteCount(s);
            if (count > MaxStringBytes)
            {
                // Cut on a character boundary, never inside a UTF-8 sequence.
                int chars = s.Length;
                while (chars > 0 && Encoding.UTF8.GetByteCount(s.ToCharArray(), 0, chars) > MaxStringBytes) chars--;
                if (chars > 0 && char.IsHighSurrogate(s[chars - 1])) chars--;
                s = s.Substring(0, chars);
                count = Encoding.UTF8.GetByteCount(s);
            }
            WriteByte((byte)count);
            Ensure(count);
            Encoding.UTF8.GetBytes(s, 0, s.Length, buf, length);
            length += count;
        }

        public void WriteRef(in NetRef r) { WriteUInt(r.id); WriteByte((byte)r.kind); }
        public void WriteRef(UnityEngine.Object o) { WriteRef(NetIds.RefOf(o)); }

        public void WritePose(in NetPose p)
        {
            WriteVector3(p.position);
            WriteAngle(p.yaw);
            WriteRotation(p.camLocalRotation);
            WriteHalf(p.height);
            byte bits = 0;
            if (p.crouching) bits |= 1;
            if (p.grounded) bits |= 2;
            if (p.throwHeld) bits |= 4;
            WriteByte(bits);
            WriteVector3Half(p.velocity);
        }
    }

    public sealed class NetReader
    {
        byte[] buf;
        int pos, end;

        public NetReader() { }
        public NetReader(byte[] buffer, int start, int count) { Reset(buffer, start, count); }

        internal void Reset(byte[] buffer, int start, int count)
        {
            buf = buffer;
            pos = start;
            end = start + count;
        }

        internal int Position => pos;

        public int Remaining => end - pos;

        void Need(int n)
        {
            if (pos + n > end) throw new NetReadException("record too short: need " + n + ", have " + (end - pos));
        }

        public byte ReadByte() { Need(1); return buf[pos++]; }
        public sbyte ReadSByte() { return (sbyte)ReadByte(); }
        public bool ReadBool() { return ReadByte() != 0; }

        public ushort ReadUShort()
        {
            Need(2);
            ushort v = (ushort)(buf[pos] | (buf[pos + 1] << 8));
            pos += 2;
            return v;
        }

        public short ReadShort() { return (short)ReadUShort(); }

        public uint ReadUInt()
        {
            Need(4);
            uint v = (uint)(buf[pos] | (buf[pos + 1] << 8) | (buf[pos + 2] << 16) | (buf[pos + 3] << 24));
            pos += 4;
            return v;
        }

        public int ReadInt() { return (int)ReadUInt(); }

        public ulong ReadULong()
        {
            ulong lo = ReadUInt();
            ulong hi = ReadUInt();
            return lo | (hi << 32);
        }

        public float ReadFloat() { return NetBits.UIntToFloat(ReadUInt()); }
        public float ReadHalf() { return Mathf.HalfToFloat(ReadUShort()); }
        public float ReadUnit() { return ReadByte() / 255f; }
        public float ReadAngle() { return ReadUShort() / 65536f * 360f; }
        public Vector3 ReadVector3() { return new Vector3(ReadFloat(), ReadFloat(), ReadFloat()); }
        public Vector3 ReadVector3Half() { return new Vector3(ReadHalf(), ReadHalf(), ReadHalf()); }
        public Quaternion ReadRotation() { return NetBits.UnpackRotation(ReadUInt()); }

        public string ReadString()
        {
            byte count = ReadByte();
            if (count == 255) return null;
            Need(count);
            string s = Encoding.UTF8.GetString(buf, pos, count);
            pos += count;
            return s;
        }

        public NetRef ReadRef()
        {
            uint id = ReadUInt();
            return new NetRef(id, (NetKind)ReadByte());
        }

        public NetPose ReadPose()
        {
            var p = new NetPose();
            p.position = ReadVector3();
            p.yaw = ReadAngle();
            p.camLocalRotation = ReadRotation();
            p.height = ReadHalf();
            byte bits = ReadByte();
            p.crouching = (bits & 1) != 0;
            p.grounded = (bits & 2) != 0;
            p.throwHeld = (bits & 4) != 0;
            p.velocity = ReadVector3Half();
            return p;
        }

        public T ReadObject<T>() where T : UnityEngine.Object
        {
            var r = ReadRef();
            return NetIds.Resolve<T>(r);
        }
    }

    public sealed class NetReadException : Exception
    {
        public NetReadException(string message) : base(message) { }
    }

    static class NetBits
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct FloatUInt
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public float f;
            [System.Runtime.InteropServices.FieldOffset(0)] public uint u;
        }

        public static uint FloatToUInt(float f) { var x = new FloatUInt { f = f }; return x.u; }
        public static float UIntToFloat(uint u) { var x = new FloatUInt { u = u }; return x.f; }

        const float Range = 0.70710678f;   // the three smallest components lie in [-1/sqrt2, 1/sqrt2]
        const int Max10 = 1023;

        public static uint PackRotation(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 1e-6f || float.IsNaN(mag)) q = Quaternion.identity;
            else { q.x /= mag; q.y /= mag; q.z /= mag; q.w /= mag; }

            int largest = 0;
            float best = Mathf.Abs(q.x);
            if (Mathf.Abs(q.y) > best) { largest = 1; best = Mathf.Abs(q.y); }
            if (Mathf.Abs(q.z) > best) { largest = 2; best = Mathf.Abs(q.z); }
            if (Mathf.Abs(q.w) > best) { largest = 3; }

            float a, b, c, big;
            switch (largest)
            {
                case 0: big = q.x; a = q.y; b = q.z; c = q.w; break;
                case 1: big = q.y; a = q.x; b = q.z; c = q.w; break;
                case 2: big = q.z; a = q.x; b = q.y; c = q.w; break;
                default: big = q.w; a = q.x; b = q.y; c = q.z; break;
            }
            if (big < 0f) { a = -a; b = -b; c = -c; }   // q and -q are the same rotation

            return ((uint)largest << 30) | (Quant(a) << 20) | (Quant(b) << 10) | Quant(c);
        }

        public static Quaternion UnpackRotation(uint packed)
        {
            int largest = (int)(packed >> 30);
            float a = Dequant((packed >> 20) & 0x3FF);
            float b = Dequant((packed >> 10) & 0x3FF);
            float c = Dequant(packed & 0x3FF);
            float big = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));
            Quaternion q;
            switch (largest)
            {
                case 0: q = new Quaternion(big, a, b, c); break;
                case 1: q = new Quaternion(a, big, b, c); break;
                case 2: q = new Quaternion(a, b, big, c); break;
                default: q = new Quaternion(a, b, c, big); break;
            }
            return q.normalized;
        }

        static uint Quant(float v)
        {
            float t = (Mathf.Clamp(v, -Range, Range) + Range) / (2f * Range);
            return (uint)Mathf.RoundToInt(t * Max10);
        }

        static float Dequant(uint v) { return v / (float)Max10 * (2f * Range) - Range; }
    }
}
