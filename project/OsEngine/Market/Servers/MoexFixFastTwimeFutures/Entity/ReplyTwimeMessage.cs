using System;
using System.Collections.Generic;


namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class ReplyTwimeMessage
    {
        private Dictionary<ushort, int> _msgLength = new Dictionary<ushort, int>()
        {
            {5001, 28},
            {5002, 17},
            {5003, 9},
            {5004, 28},
            {5005, 28},
            {5006, 16},
            {5007, 24},
            {5008, 21},
            {5009, 28},
            {7007, 28},
            {7010, 20},
            {7014, 29},
            {7015, 82},
            {7017, 60},
            {7018, 77},
            {7019, 85}

        };

        public Dictionary<ushort, int> MsgLength
        {
            get { return _msgLength; }
        }

        public List<byte[]> GetMessagesArrays(byte[] bufferMsg)
        {
            List<byte[]> byteArrays = new List<byte[]>();
            ushort messageId = BitConverter.ToUInt16(bufferMsg, 2);

            int startIndex = 0;

            do
            {
                int length = _msgLength[messageId];
                byte[] oneMsg = new byte[length];
                Array.Copy(bufferMsg, startIndex, oneMsg, 0, length);
                byteArrays.Add(oneMsg);

                startIndex += length;

                if (startIndex < bufferMsg.Length)
                    messageId = BitConverter.ToUInt16(bufferMsg, startIndex + 2);
                else
                    return byteArrays;
            }
            while (_msgLength.ContainsKey(messageId));

            return byteArrays;
        }

    }

}
