using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OsEngine.Market.Servers.Entity
{
    static class MarshalUtf8
    {
        private static UTF8Encoding _utf8;

        //--------------------------------------------------------------------------------
        static MarshalUtf8()
        {
            _utf8 = new UTF8Encoding();
        }

        //--------------------------------------------------------------------------------
        public static IntPtr StringToHGlobalUtf8(String data)
        {
            Byte[] dataEncoded = _utf8.GetBytes(data + "\0");

            int size = Marshal.SizeOf(dataEncoded[0]) * dataEncoded.Length;

            IntPtr pData = Marshal.AllocHGlobal(size);

            Marshal.Copy(dataEncoded, 0, pData, dataEncoded.Length);

            return pData;
        }

        //--------------------------------------------------------------------------------
        public static String PtrToStringUtf8(IntPtr pData)
        {
            // this is just to get buffer length in bytes
            String errStr = Marshal.PtrToStringAnsi(pData);
            int length = errStr.Length;

            Byte[] data = new byte[length];
            Marshal.Copy(pData, data, 0, length);

            return _utf8.GetString(data);
        }
        //--------------------------------------------------------------------------------
    }
}
