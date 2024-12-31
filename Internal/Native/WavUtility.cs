/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dtl.Internal.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WavHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] ChunkID;
        public uint ChunkSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Format;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Subchunk1ID;
        public uint Subchunk1Size;
        public ushort AudioFormat;
        public ushort NumChannels;
        public uint SampleRate;
        public uint ByteRate;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Subchunk2ID;
        public uint Subchunk2Size;
    }

    public static unsafe class WavUtility
    {
        public static byte[] Export(Array data, int freq, byte channels, int bitsPerSample)
        {
            if (bitsPerSample != 8 && bitsPerSample != 16)
                throw new ArgumentException("Only 8-bit and 16-bit samples are supported.");

            int bytesPerSample = bitsPerSample / 8;

            WavHeader header = new()
            {
                ChunkID = "RIFF"u8.ToArray(),
                Format = "WAVE"u8.ToArray(),
                Subchunk1ID = "fmt "u8.ToArray(),
                Subchunk1Size = 16,
                AudioFormat = 1,
                NumChannels = channels,
                SampleRate = (uint)freq,
                ByteRate = (uint)(freq * channels * bytesPerSample),
                BlockAlign = (ushort)(channels * bytesPerSample),
                BitsPerSample = (ushort)bitsPerSample,
                Subchunk2ID = "data"u8.ToArray(),
                Subchunk2Size = (uint)(data.Length * bytesPerSample)
            };

            header.ChunkSize = 36 + header.Subchunk2Size;

            byte[] wavFile = new byte[Marshal.SizeOf(header) + data.Length * bytesPerSample];

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(header));
            Marshal.StructureToPtr(header, ptr, false);
            Marshal.Copy(ptr, wavFile, 0, Marshal.SizeOf(header));
            Marshal.FreeHGlobal(ptr);

            // Unsafe context for copying data
            fixed (byte* p = &wavFile[Marshal.SizeOf(header)])
            {
                switch (bitsPerSample)
                {
                    case 16:
                        {
                            short[] shortData = (short[])data;

                            fixed (short* pData = &shortData[0])
                            {
                                short* pSrc = pData;
                                byte* pDst = p;

                                for (int i = 0; i < shortData.Length; i++)
                                {
                                    *(short*)pDst = *pSrc;
                                    pDst += 2;
                                    pSrc++;
                                }
                            }

                            break;
                        }
                    case 8:
                        {
                            byte[] byteData = (byte[])data;

                            fixed (byte* pData = &byteData[0])
                            {
                                byte* pSrc = pData;
                                byte* pDst = p;

                                for (int i = 0; i < byteData.Length; i++)
                                {
                                    *pDst = *pSrc;
                                    pDst++;
                                    pSrc++;
                                }
                            }

                            break;
                        }
                }
            }

            return wavFile;
        }
    }

}
