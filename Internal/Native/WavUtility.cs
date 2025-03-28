/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dtl.Internal.Native {
    public static unsafe class WavUtility {
        public static byte[] Export<T>(T data, uint freq, byte channels) {
            byte bitsPerSample = GetBpsValue<T>();
            ushort bytesPerSample = (ushort)(bitsPerSample / 8);
            uint sampleCount = GetLength(data);
            uint dataSize = sampleCount * bytesPerSample;

            byte[] wavFile = new byte[44 + dataSize];
            fixed (byte* ptr = wavFile) {
                byte* h = ptr;
                WriteId(h, "RIFF"); h += 4;
                *(uint*)h = 36 + dataSize; h += 4;
                WriteId(h, "WAVE"); h += 4;
                WriteId(h, "fmt "); h += 4;
                *(uint*)h = 16; h += 4;
                *(ushort*)h = 1; h += 2;
                *(ushort*)h = channels; h += 2;
                *(uint*)h = freq; h += 4;
                *(uint*)h = freq * channels * bytesPerSample; h += 4;
                *(ushort*)h = (ushort)(channels * bytesPerSample); h += 2;
                *(ushort*)h = bitsPerSample; h += 2;
                WriteId(h, "data"); h += 4;
                *(uint*)h = dataSize; h += 4;

                byte* audioDst = ptr + 44;
                switch (bitsPerSample) {
                    case 16: {
                        short[] shortData = data as short[]
                            ?? throw new ArgumentException("input array must be short[] (16 bit)");

                        fixed (short* pData = shortData)
                            Buffer.MemoryCopy(pData, audioDst, dataSize, dataSize);

                        break;
                    }
                    case 8: {
                        byte[] byteData = data as byte[]
                            ?? throw new ArgumentException("input array must be byte[] (8 bit)");

                        fixed (byte* pData = byteData)
                            Buffer.MemoryCopy(pData, audioDst, dataSize, dataSize);

                        break;
                    }
                    default:
                        throw new ArgumentException("8 and 16 bit is only supported");
                }
            }
            return wavFile;
        }

        static unsafe void WriteId(byte* destination, string source) {
            if (source.Length < 4)
                throw new ArgumentException("riff id string must be 4 characters long.");
            for (int i = 0; i < 4; i++)
                *(destination + i) = (byte)source[i];
        }

        static byte GetBpsValue<T>() {
            if (typeof(T).GetElementType() == typeof(short)) return 16;
            if (typeof(T).GetElementType() == typeof(byte)) return 8;
            throw new ArgumentException("unsupported audio type");
        }

        static uint GetLength<T>(T data) {
            if (data is Array array)
                return (uint)array.LongLength;
            else if (data is IEnumerable<object> enumerable)
                return (uint)enumerable.LongCount();

            return 0;
        }
    }

}
