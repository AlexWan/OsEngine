using System;
using System.Collections.Generic;
using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class TwimeMessageConstructor
    {
        private const ushort SCHEMAID = 19781;
        private const ushort VERSION = 7;

        public byte[] Establish(uint KeepaliveInterval, string Credentials, out string msgLog)
        {
            ushort lengthMsg = 32;
            ushort idMsg = 5000;

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            byte[] additionalBytes = null;

            DateTime utcDateTime = DateTime.UtcNow;
            ulong unixTimeSeconds = (ulong)(utcDateTime - new DateTime(1970, 1, 1)).TotalSeconds;
            ulong unixTimeNanoseconds = unixTimeSeconds * 1_000_000_000;

            msgBytes.AddRange(BitConverter.GetBytes(unixTimeNanoseconds));
            msgBytes.AddRange(BitConverter.GetBytes(KeepaliveInterval));

            byte[] loginBytes = Encoding.UTF8.GetBytes(Credentials);

            if (loginBytes.Length < 20)
            {
                additionalBytes = new byte[20 - loginBytes.Length];

                byte n = 0;

                for (int i = 0; i < additionalBytes.Length; i++)
                {
                    additionalBytes[i] = n;
                }
            }

            msgBytes.AddRange(loginBytes);

            if (additionalBytes != null)
                msgBytes.AddRange(additionalBytes);

            StringBuilder body = new StringBuilder(); // логи для сертификации
            body.Append("Establish(");
            body.Append(headMsgLog + ", ");
            body.Append("Timestamp=" + unixTimeNanoseconds + ", ");
            body.Append("KeepaliveInterval=" + KeepaliveInterval + ", ");
            body.Append("Credentials=" + Credentials + ")");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] Terminate(out string msgLog)
        {
            ushort lengthMsg = 1;
            ushort idMsg = 5003;
            byte TerminationCode = 0;

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            msgBytes.Add(TerminationCode);

            StringBuilder body = new StringBuilder();
            body.Append("Terminate(");
            body.Append(headMsgLog + ", ");
            body.Append("TerminationCode=" + TerminationCode + ")");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] Sequence(out string msgLog)
        {
            ushort lengthMsg = 8;
            ushort idMsg = 5006;
            ulong NextSeqNo = 18446744073709551615; // nullValue

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            msgBytes.AddRange(BitConverter.GetBytes(NextSeqNo));

            StringBuilder body = new StringBuilder();
            body.Append("Sequence(");
            body.Append(headMsgLog + ", ");
            body.Append("NextSeqNo=" + NextSeqNo + ")");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] RetransmitRequest(ulong fromSeqNo, uint count, out string msgLog)
        {
            ushort lengthMsg = 20;
            ushort idMsg = 5004;

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            DateTime utcDateTime = DateTime.UtcNow;
            ulong unixTimeSeconds = (ulong)(utcDateTime - new DateTime(1970, 1, 1)).TotalSeconds;
            ulong unixTimeNanoseconds = unixTimeSeconds * 1_000_000_000;

            msgBytes.AddRange(BitConverter.GetBytes(unixTimeNanoseconds));
            msgBytes.AddRange(BitConverter.GetBytes(fromSeqNo));
            msgBytes.AddRange(BitConverter.GetBytes(count));

            StringBuilder body = new StringBuilder();
            body.Append("RetransmitRequest(");
            body.Append(headMsgLog + ", ");
            body.Append("Timestamp=" + unixTimeNanoseconds + ", ");
            body.Append("FromSeqNo=" + fromSeqNo + ", ");
            body.Append("Count=" + count + ")");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] NewOrderSingle(ulong ClOrdId, ulong ExpireDate, decimal price, int securityId, int ClOrdLinkId, int OrderQty, byte TimeInForce, byte side, string account, out string msgLog)
        {
            ushort lengthMsg = 47;
            ushort idMsg = 6000;

            string ComplianceId = " ";
            byte ClientFlags = 0;
            long mantissaPrice = (long)(price * 100000);

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            byte[] additionalBytes = null;

            msgBytes.AddRange(BitConverter.GetBytes(ClOrdId));
            msgBytes.AddRange(BitConverter.GetBytes(ExpireDate));
            msgBytes.AddRange(BitConverter.GetBytes(mantissaPrice));
            msgBytes.AddRange(BitConverter.GetBytes(securityId));
            msgBytes.AddRange(BitConverter.GetBytes(ClOrdLinkId));
            msgBytes.AddRange(BitConverter.GetBytes(OrderQty));
            msgBytes.AddRange(Encoding.UTF8.GetBytes(ComplianceId));
            msgBytes.Add(TimeInForce);
            msgBytes.Add(side);
            msgBytes.Add(ClientFlags);

            byte[] accountBytes = Encoding.UTF8.GetBytes(account);

            if (accountBytes.Length < 7)
            {
                additionalBytes = new byte[7 - accountBytes.Length];

                byte n = 0;

                for (int i = 0; i < additionalBytes.Length; i++)
                {
                    additionalBytes[i] = n;
                }
            }

            msgBytes.AddRange(accountBytes);

            if (additionalBytes != null)
                msgBytes.AddRange(additionalBytes);

            StringBuilder body = new StringBuilder();
            body.Append("NewOrderSingle(");
            body.Append(headMsgLog + ", ");
            body.Append("ClOrdId=" + ClOrdId + ", ");
            body.Append("ExpireDate=" + ExpireDate + ", ");
            body.Append("Price=" + price + ", ");
            body.Append("SecurityId=" + securityId + ", ");
            body.Append("ClOrdLinkId=" + ClOrdLinkId + ", ");
            body.Append("OrderQty=" + OrderQty + ", ");
            body.Append("ComplianceId=" + ComplianceId + ", ");
            body.Append("TimeInForce=" + TimeInForce + ", ");
            body.Append("Side=" + side + ", ");
            body.Append("ClientFlags=" + ClientFlags + ", ");
            body.Append("Account=" + account + ")");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] OrderCancel(ulong ClOrdId, long OrderID, int securityId, string account, out string msgLog)
        {
            ushort lengthMsg = 28;
            ushort idMsg = 6006;
            byte ClientFlags = 0;

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            byte[] additionalBytes = null;

            msgBytes.AddRange(BitConverter.GetBytes(ClOrdId));
            msgBytes.AddRange(BitConverter.GetBytes(OrderID));
            msgBytes.AddRange(BitConverter.GetBytes(securityId));
            msgBytes.Add(ClientFlags);

            byte[] accountBytes = Encoding.UTF8.GetBytes(account);

            if (accountBytes.Length < 7)
            {
                additionalBytes = new byte[7 - accountBytes.Length];

                byte n = 0;

                for (int i = 0; i < additionalBytes.Length; i++)
                {
                    additionalBytes[i] = n;
                }
            }

            msgBytes.AddRange(accountBytes);

            if (additionalBytes != null)
                msgBytes.AddRange(additionalBytes);

            StringBuilder body = new StringBuilder();
            body.Append("OrderCancelRequest(");
            body.Append(headMsgLog + ", ");
            body.Append("ClOrdID=" + ClOrdId + ", ");
            body.Append("OrderID=" + OrderID + ", ");
            body.Append("SecurityID=" + securityId + ", ");
            body.Append("ClientFlags=" + ClientFlags + ", ");
            body.Append("Account=" + account + ", ");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] OrderMassCancel(ulong clOrdID, int clOrdLinkID, int securityID, byte securityType, byte side, string account, string securityGroup, out string msgLog)
        {
            ushort lengthMsg = 50;
            ushort idMsg = 6004;
            // byte ClientFlags = 1;

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            byte[] additionalBytes = null;

            msgBytes.AddRange(BitConverter.GetBytes(clOrdID));
            msgBytes.AddRange(BitConverter.GetBytes(clOrdLinkID));
            msgBytes.AddRange(BitConverter.GetBytes(securityID));
            msgBytes.Add(securityType);
            msgBytes.Add(side);

            byte[] accountBytes = Encoding.UTF8.GetBytes(account);

            if (accountBytes.Length < 7)
            {
                additionalBytes = new byte[7 - accountBytes.Length];

                byte n = 0;

                for (int i = 0; i < additionalBytes.Length; i++)
                {
                    additionalBytes[i] = n;
                }
            }

            msgBytes.AddRange(accountBytes);

            if (additionalBytes != null)
                msgBytes.AddRange(additionalBytes);

            byte[] securityGroupBytes = Encoding.UTF8.GetBytes(securityGroup);

            if (securityGroupBytes.Length < 25)
            {
                additionalBytes = new byte[25 - securityGroupBytes.Length];

                byte n = 0;

                for (int i = 0; i < additionalBytes.Length; i++)
                {
                    additionalBytes[i] = n;
                }
            }

            msgBytes.AddRange(securityGroupBytes);

            if (additionalBytes != null)
                msgBytes.AddRange(additionalBytes);

            StringBuilder body = new StringBuilder();
            body.Append("OrderMassCancelRequest(");
            body.Append(headMsgLog + ", ");
            body.Append("ClOrdID=" + clOrdID + ", ");
            body.Append("ClOrdLinkID=" + clOrdLinkID + ", ");
            body.Append("SecurityID=" + securityID + ", ");
            body.Append("SecurityType=" + securityType + ", ");
            body.Append("Side=" + side + ", ");
            body.Append("Account=" + account + ", ");
            body.Append("SecurityGroup=" + securityGroup);
            body.Append("LengthArray=" + msgBytes.Count);
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public byte[] OrderReplace(ulong clOrdId, long orderID, decimal newPrice, uint orderQty, int clOrdLinkID, int securityID, string account, out string msgLog)
        {
            ushort lengthMsg = 46;
            ushort idMsg = 6007;

            string ComplianceId = " ";
            byte ClientFlags = 0;
            long mantissaPrice = (long)(newPrice * 100000);
            byte Mode = 0;

            List<byte> msgBytes = new List<byte>();

            msgBytes.AddRange(GetTwimeMessageHeader(lengthMsg, idMsg, out string headMsgLog));

            byte[] additionalBytes = null;

            msgBytes.AddRange(BitConverter.GetBytes(clOrdId));
            msgBytes.AddRange(BitConverter.GetBytes(orderID));
            msgBytes.AddRange(BitConverter.GetBytes(mantissaPrice));
            msgBytes.AddRange(BitConverter.GetBytes(orderQty));
            msgBytes.AddRange(BitConverter.GetBytes(clOrdLinkID));
            msgBytes.AddRange(BitConverter.GetBytes(securityID));
            msgBytes.AddRange(Encoding.UTF8.GetBytes(ComplianceId));
            msgBytes.Add(Mode);
            msgBytes.Add(ClientFlags);

            byte[] accountBytes = Encoding.UTF8.GetBytes(account);

            if (accountBytes.Length < 7)
            {
                additionalBytes = new byte[7 - accountBytes.Length];

                byte n = 0;

                for (int i = 0; i < additionalBytes.Length; i++)
                {
                    additionalBytes[i] = n;
                }
            }

            msgBytes.AddRange(accountBytes);

            if (additionalBytes != null)
                msgBytes.AddRange(additionalBytes);

            StringBuilder body = new StringBuilder();

            body.Append("OrderReplaceRequest(");
            body.Append(headMsgLog + ", ");
            body.Append("ClOrdID=" + clOrdId + ", ");
            body.Append("OrderID=" + orderID + ", ");
            body.Append("Price=" + newPrice + ", ");
            body.Append("OrderQty=" + orderQty + ", ");
            body.Append("ClOrdLinkID=" + clOrdLinkID + ", ");
            body.Append("SecurityID=" + securityID + ", ");
            body.Append("ComplianceId=" + ComplianceId + ", ");
            body.Append("Mode=" + Mode + ", ");
            body.Append("ClientFlags=" + ClientFlags + ", ");
            body.Append("Account=" + account + ")");
            msgLog = body.ToString();

            return msgBytes.ToArray();
        }

        public List<byte> GetTwimeMessageHeader(ushort blockLength, ushort templateId, out string headMsgLog)
        {
            List<byte> headerBytes = new List<byte>();

            headerBytes.AddRange(BitConverter.GetBytes(blockLength));
            headerBytes.AddRange(BitConverter.GetBytes(templateId));
            headerBytes.AddRange(BitConverter.GetBytes(SCHEMAID));
            headerBytes.AddRange(BitConverter.GetBytes(VERSION));

            StringBuilder header = new StringBuilder();
            header.Append("blockLength=" + blockLength + ", ");
            header.Append("templateId=" + templateId + ", ");
            header.Append("schemaId=" + SCHEMAID + ", ");
            header.Append("version=" + VERSION);

            headMsgLog = header.ToString();

            return headerBytes;
        }
    }
}
