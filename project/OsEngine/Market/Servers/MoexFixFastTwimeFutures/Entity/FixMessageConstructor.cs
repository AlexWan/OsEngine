using System;
using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class FixMessageConstructor
    {
        public enum MessageType
        {
            Logon,
            Logout,
            Heartbeat,
            TestRequest,
            ResendRequest,
            Reject,
            SequenceReset,
            NewOrder,
            OrderStatusRequest,
            OrderCancel,
            OrderMassCancel,
            OrderReplace,
            MarketDataRequest
        }

        private string _senderCompID;
        private string _targetCompID;

        public FixMessageConstructor(string senderCompID, string targetCompID)
        {
            _senderCompID = senderCompID;
            _targetCompID = targetCompID;
        }


        public string MarketDataRequestMessage(int messageSequenceNumber, string MDReqId, long missingBeginSeqNo, long missingEndSeqNo)
        {
            StringBuilder body = new StringBuilder();

            body.Append("262=" + MDReqId + "|");
            body.Append("1182=" + missingBeginSeqNo + "|");
            body.Append("1183=" + missingEndSeqNo + "|");

            string header = ConstructFASTHeader(SessionMessageCode(MessageType.MarketDataRequest), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Создать новый ордер
        /// </summary>
        /// <param name="clordId">11. Уникальный  пользовательский  идентификатор  заявки, установленный  торгующей  организацией  или  инвестором, интересы которого представляет посредническая организация.</param>
        /// <param name="partiesParams">Стороны заявки. Обычно содержит код клиента. </param>
        /// <param name="symbol">55. идентификатор финансового  инструмента. </param>
        /// <param name="CFICode">461. Класс финансового инструмента по стандарту ISO-10962. FXXXXX – фьючерс,  OXXXXX – опцион,  FMXXXS – календарный спред</param>
        /// <param name="securityType">167. Тип составного инструмента.</param>
        /// <param name="account">1. Торговый счет, в счет которого подается заявка.</param>

        /// <param name="DisplayQty">1138. Всплывающая (видимая часть) айсберг-заявки. (Для заявок типа Айсберг).</param>
        /// <param name="aceberg">Тип заявки Айсберг?</param>
        /// <param name="DisplayVarianceQty">20036.Величина  случайного  отклонения  объема  всплывающей  части  айсберг-заявки.</param>
        /// <param name="marketSegmentID">1300. Сегмент рынка, для которого предназначена заявка.</param>
        /// <param name="side">56. Направление заявки. '1' (Покупка)'2' (Продажа)</param>
        /// <param name="ordType">40. Тип заявки. '1' (Рыночная)'2' (Лимитная)</param>
        /// <param name="price">Цена  заявки,  используется  для  лимитной  заявки.  Поле обязательное, если задано в заявке. Для рыночных заявок должно быть заполнено 0.</param>
        /// <returns></returns>
        public string NewOrderMessage(string clordId, string ordType, string symbol, string CFICode, string account, string timeInForce, string side, bool aceberg,
                                         string DisplayQty, string DisplayVarianceQty, string orderQty, string price,
                                         string[] partiesParams, string ExpireDate, string marketSegmentID, long messageSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            //YYYYMMDD- HH:MM:SS.sssssssss
            body.Append("60=" + DateTime.UtcNow.ToString("yyMMdd-HH:mm:ss.fffffff").Insert(16, "00") + "|");
            body.Append("11=" + clordId + "|");

            body.Append("40=" + ordType + "|");
            body.Append("55=" + symbol + "|");

            if (CFICode != null)
                body.Append("461=" + CFICode + "|");

            // body.Append("167=" + securityType + "|"); обязательно для календарных спредов

            body.Append("1=" + account + "|");
            body.Append("59=" + timeInForce + "|");
            body.Append("54=" + side + "|");

            //iceberg
            if (aceberg)
            {
                body.Append("1138=" + DisplayQty + "|");
                body.Append("20036=" + DisplayVarianceQty + "|");
                body.Append("1084=3|"); //DisplayMethod" 3  -  Random  (randomize  value)
            }

            body.Append("38=" + orderQty + "|");

            if (ordType == "1")
                body.Append("44=0|");
            else
                body.Append("44=" + price + "|");

            if (partiesParams != null)
                body.Append(ConstructParties(partiesParams));

            if (timeInForce.Equals("6"))
                body.Append("432=" + ExpireDate + "|");

            body.Append("1300=" + marketSegmentID + "|");

            string header = ConstructHeader(SessionMessageCode(MessageType.NewOrder), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Стороны заявки. Обычно содержит код клиента.
        /// </summary>
        /// <returns></returns>
        public string ConstructParties(string[] partiesParams)
        {

            StringBuilder msg = new StringBuilder();
            msg.Append("453=" + partiesParams[0] + "|");
            msg.Append("448=" + partiesParams[1] + "|");
            msg.Append("447=" + partiesParams[2] + "|");
            msg.Append("452=" + partiesParams[3] + "|");

            return msg.ToString();
        }

        /// <summary>
        /// Запрос текущего состояния конкретной заявки
        /// </summary>
        /// <param name="ClOrdId">Пользовательский идентификатор заявки, состояние которой запрашивается.</param>
        /// <param name="orderID">Идентификатор заявки в системе SPECTRA.</param>
        /// <param name="side">Направление сделки</param>
        /// <param name="messageSequenceNumber">Номер исходящих сообщений</param>
        public string OrderStatusRequest(string ClOrdId, string orderID, string symbol, string side, long messageSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            body.Append("11=" + ClOrdId + "|");

            if (orderID != string.Empty)
                body.Append("37=" + orderID + "|");

            body.Append("55=" + symbol + "|");
            body.Append("54=" + side + "|");

            string header = ConstructHeader(SessionMessageCode(MessageType.OrderStatusRequest), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }


        /// <summary>
        /// Отмена/снятие ранее размещенной заявки F
        /// </summary>
        /// <param name="origClOrdId">Пользовательский  идентификатор  заявки,  которую  надо  снять.</param>
        /// <param name="orderID">Биржевой номер заявки, которую надо снять. </param>
        /// <param name="clordId">Уникальный идентификатор сообщения Order Cancel Request (F) - запроса на снятие заявки.</param>
        /// <param name="side">Направление сделки</param>
        /// <param name="messageSequenceNumber">Номер исходящих сообщений</param>
        public string OrderCanselMessage(string[] partiesParams, string clordId, string orderID, string origClOrdId, string symbol, string CFICode, string side, string orderQty, long messageSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            if (partiesParams != null)
                body.Append(ConstructParties(partiesParams));

            body.Append("11=" + clordId + "|");
            body.Append("37=" + orderID + "|");
            body.Append("41=" + origClOrdId + "|");
            body.Append("55=" + symbol + "|");

            if (CFICode != null)
                body.Append("461=" + CFICode + "|");

            body.Append("54=" + side + "|");
            body.Append("60=" + DateTime.UtcNow.ToString("yyMMdd-HH:mm:ss.fffffff").Insert(16, "00") + "|");
            body.Append("38=" + orderQty + "|");

            string header = ConstructHeader(SessionMessageCode(MessageType.OrderCancel), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Снять все заявки
        /// </summary>
        /// <param name="clordId">Уникальный идентификатор сообщения</param>
        /// <param name="massCancelRequestType">Тип массового запроса на снятие заявок.</param>
        /// <param name="tradingSessionID">если тип запроса - 7, то не нужен</param>
        /// <param name="symbol">если тип запроса - 7, то не нужен</param>
        /// <param name="account"></param>
        /// <param name="messageSequenceNumber"></param>
        /// <returns></returns>
        public string OrderMassCanselMessage(string[] partiesParams, string clordId, string massCancelRequestType, string marketSegmentID, string account, string symbol, long messageSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            if (partiesParams != null)
                body.Append(ConstructParties(partiesParams));

            body.Append("11=" + clordId + "|");
            body.Append("530=" + massCancelRequestType + "|");

            if (massCancelRequestType == "1")
                body.Append("1300=" + marketSegmentID + "|");

            if (massCancelRequestType == "8" || massCancelRequestType == "9")
                body.Append("1300=" + marketSegmentID + "|");

            body.Append("1=" + account + "|");

            if (massCancelRequestType == "1")
                body.Append("55=" + symbol + "|");


            body.Append("60=" + DateTime.UtcNow.ToString("yyMMdd-HH:mm:ss.fffffff").Insert(16, "00") + "|");


            string header = ConstructHeader(SessionMessageCode(MessageType.OrderMassCancel), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Изменение параметров заявки. Можно изменить цену заявки (44), количество (38), или поле SecondaryClOrdID
        /// </summary>
        /// <param name="clordId">Уникальный идентификатор сообщения на замену заявки.(11)</param>
        /// <param name="origClOrdId">Пользовательский   идентификатор   заявки,   которую   надо   изменить(41).</param>
        /// <param name="orderID">Биржевой номер заявки, которую надо снять.</param>
        /// <param name="account">Торговый счет. Должен совпадать с номером счета исходной заявки.</param>
        /// <param name="instrumentParams">Финансовый инструмент, по которому была подана изменяемая заявка. Должно совпадать со значением в исходной заявке.</param>
        /// <param name="price">Цена за единицу ценной бумаги. </param>
        /// <param name="orderQty">Количество ценных бумаг, выраженное в лотах</param>
        /// <param name="side">Направленность  заявки.  Должно  совпадать  со  значением  в  исходной заявке.</param>
        /// <param name="messageSequenceNumber"></param>
        /// <param name="tradingSessionID">Идентификатор режима торгов для финансового инструмента, заявки по которому  должны  быть  изменены. </param>
        /// <param name="ordType">Тип изменяемой заявки. Должен быть тем же, что и в исходной заявке.</param>
        /// <param name="noTradingSessions"></param>
        /// <returns></returns>
        public string OrderReplaceMessage(string[] partiesParams, string clordId, string orderID, string origClOrdId, string orderQty,
                                          string price, string symbol, string CFICode, string side,
                                          long messageSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            if (partiesParams != null)
                body.Append(ConstructParties(partiesParams));

            body.Append("11=" + clordId + "|");
            body.Append("37=" + orderID + "|");
            body.Append("41=" + origClOrdId + "|");
            body.Append("38=" + orderQty + "|");
            body.Append("44=" + price + "|");
            body.Append("55=" + symbol + "|");

            if (CFICode != null)
                body.Append("461=" + CFICode + "|");

            body.Append("54=" + side + "|");
            body.Append("60=" + DateTime.UtcNow.ToString("yyMMdd-HH:mm:ss.fffffff").Insert(16, "00") + "|");

            string header = ConstructHeader(SessionMessageCode(MessageType.OrderReplace), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Logon
        /// </summary>
        /// <param name="resetSeqNum">Индикатор,  указывающий  должны  ли  обе  стороны  сбросить  счетчики сообщений. По умолчанию "Нет"</param>
        public string LogonMessage(long messageSequenceNumber, int heartBeatSeconds, bool resetSeqNum)
        {
            StringBuilder body = new StringBuilder();

            body.Append("98=0|");
            body.Append("108=" + heartBeatSeconds + "|");

            if (resetSeqNum)
                body.Append("141=Y|");
            else body.Append("141=N|");

            string header = ConstructHeader(SessionMessageCode(MessageType.Logon), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// For data recovery
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="messageSequenceNumber"></param>
        public string LogonFASTMessage(string userName, string password, int messageSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            body.Append("553=" + userName + "|");
            body.Append("554=" + password + "|");
            // body.Append("1137=9|"); // Версия протокола на сессионном уровне. 

            string header = ConstructFASTHeader(SessionMessageCode(MessageType.Logon), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Сообщение Heartbeat (0) используется для контроля состояния соединения.
        /// Если Heartbeat(0) сообщение посылается в ответ на Test Request(1) сообщение, то в первом - поле TestReqID(112) 
        /// должно содержать идентификатор Test Request(1) сообщения, на которое оно является ответом.
        /// Это используется для того, чтобы определить является ли Heartbeat(0) сообщение ответом на Test Request(1) сообщение.
        /// </summary>
        /// <param name="messageSequenceNumber"></param>
        /// <param name="isResponce">Является ли ответом на Test Request?</param>
        public string HeartbeatMessage(long messageSequenceNumber, bool isResponce, string testReqID)
        {
            StringBuilder body = new StringBuilder();
            if (isResponce)
                body.Append($"112={testReqID}|");

            string header = ConstructHeader(SessionMessageCode(MessageType.Heartbeat), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Test Request (1) сообщение вызывает/инициирует/запрашивает Heartbeat (0) сообщение с противоположной стороны.
        /// Сообщение Test Request (1) проверяет порядковые номера или проверяет состояние соединения. 
        /// На Test Request (1) сообщение противоположная сторона отвечает Heartbeat (0) сообщением,
        /// в котором TestReqID (112) – идентификатор (1) сообщения.
        /// </summary>
        /// <param name="messageSequenceNumber"></param>
        /// <param name="testRequestID"></param>
        public string TestRequestMessage(long messageSequenceNumber, string testRequestID)
        {
            StringBuilder body = new StringBuilder();

            body.Append("112=" + testRequestID + "|");
            string header = ConstructHeader(SessionMessageCode(MessageType.TestRequest), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        public string LogoutMessage(long messageSequenceNumber)
        {
            string header = ConstructHeader(SessionMessageCode(MessageType.Logout), messageSequenceNumber, string.Empty);
            string trailer = ConstructTrailer(header);
            string headerAndMessageAndTrailer = header + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        public string LogoutFASTMessage(int messageSequenceNumber)
        {
            string header = ConstructFASTHeader(SessionMessageCode(MessageType.Logout), messageSequenceNumber, string.Empty);
            string trailer = ConstructTrailer(header);
            string headerAndMessageAndTrailer = header + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Сообщение Resend Request (2) используется для инициирования повторной пересылки сообщений.
        /// Эта функция используется в случаях, если обнаружено расхождение в порядковых номерах сообщений или как функция процесса инициализации. 
        /// </summary>
        /// <param name="messageSequenceNumber"></param>
        /// <param name="beginSequenceNo">Номер первого сообщения, которое нужно повторно переслать.(7)</param>
        /// <param name="endSequenceNo">Номер последнего сообщения, которое нужно повторно переслать.(16)</param>
        public string ResendMessage(long messageSequenceNumber, long beginSequenceNo, long endSequenceNo)
        {
            StringBuilder body = new StringBuilder();
            body.Append("7=" + beginSequenceNo + "|");
            body.Append("16=" + endSequenceNo + "|");
            string header = ConstructHeader(SessionMessageCode(MessageType.ResendRequest), messageSequenceNumber, body.ToString());
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Передается в обоих направлениях. 
        /// Указывает на неверно переданное или недопустимое сообщение сессионного уровня, пришедшее от противоположной стороны.
        /// </summary>
        /// <param name="messageSequenceNumber"></param>
        /// <param name="rejectSequenceNumber">Номер отклоняемого сообщения</param>
        public string RejectMessage(long messageSequenceNumber, long rejectSequenceNumber)
        {
            StringBuilder body = new StringBuilder();

            body.Append("45=" + rejectSequenceNumber + "|");
            string header = ConstructHeader(SessionMessageCode(MessageType.Reject), messageSequenceNumber, string.Empty);
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Сообщение Sequence Reset (4) имеет следующие режимы:
        ///  - Режим заполнения пробелов(используется поле MsgSeqNum);
        ///  - Режим сбрасывания счетчиков (поле MsgSeqNum игнорируется).
        /// </summary>
        /// <param name="messageSequenceNumber"></param>
        /// <param name="newSequenceNumber">Новый порядковый номер</param>
        /// <param name="fillingGaps">Режим заполнения пробелов(true)/Режим сбрасывания счетчиков(false)</param>
        public string SequenceResetMessage(long messageSequenceNumber, int newSequenceNumber, bool fillingGaps)
        {
            StringBuilder body = new StringBuilder();

            if (fillingGaps)
                body.Append("123=Y|");
            else
                body.Append("123=N|");

            body.Append("36=" + newSequenceNumber + "|");

            string header = ConstructHeader(SessionMessageCode(MessageType.SequenceReset), messageSequenceNumber, string.Empty);
            string headerAndBody = header + body;
            string trailer = ConstructTrailer(headerAndBody);
            string headerAndMessageAndTrailer = header + body + trailer;
            return headerAndMessageAndTrailer.Replace("|", "\u0001");
        }

        /// <summary>
        /// Constructs the message header.
        /// </summary>
        /// <param name="type">The message type.</param>
        /// <param name="messageSequenceNumber">The message sequence number.</param>
        /// <param name="bodyMessage">The body message.</param>
        private string ConstructHeader(string type, long messageSequenceNumber, string bodyMessage)
        {
            StringBuilder header = new StringBuilder();

            header.Append("8=FIX.4.4|");

            StringBuilder message = new StringBuilder();

            message.Append("35=" + type + "|");
            message.Append("49=" + _senderCompID + "|");
            message.Append("56=" + _targetCompID + "|");
            message.Append("34=" + messageSequenceNumber + "|");
            message.Append("52=" + DateTime.UtcNow.ToString("yyMMdd-HH:mm:ss.fffffff").Insert(16, "00") + "|");

            int length = message.Length + bodyMessage.Length;

            header.Append("9=" + length + "|");
            header.Append(message);
            return header.ToString();
        }

        private string ConstructFASTHeader(string type, int messageSequenceNumber, string bodyMessage)
        {
            StringBuilder header = new StringBuilder();

            header.Append("8=FIXT.1.1|");

            StringBuilder message = new StringBuilder();

            message.Append("34=" + messageSequenceNumber + "|");
            message.Append("35=" + type + "|");
            message.Append("49=" + _senderCompID + "|");
            message.Append("52=" + DateTime.UtcNow.ToString("yyMMddHHmmssffffff") + "|");
            message.Append("1128=9|");// Определяет версию протокола (FIX50SP2)

            // message.Append("56=" + _targetCompID + "|");



            int length = message.Length + bodyMessage.Length;

            header.Append("9=" + length + "|");
            header.Append(message);
            return header.ToString();
        }

        private string ConstructTrailer(string message)
        {
            string trailer = "10=" + CalculateChecksum(message.Replace("|", "\u0001").ToString()).ToString().PadLeft(3, '0') + "|";
            return trailer;
        }

        private int CalculateChecksum(string dataToCalculate)
        {
            byte[] byteToCalculate = Encoding.ASCII.GetBytes(dataToCalculate);

            int checksum = 0;

            for (int i = 0; i < byteToCalculate.Length; i++)
            {
                checksum += byteToCalculate[i];
            }

            return checksum % 256;
        }

        /// <summary>
        /// Возврат кода сообщения
        /// </summary>
        private string SessionMessageCode(MessageType type)
        {
            switch (type)
            {
                case MessageType.Heartbeat:
                    return "0";

                case MessageType.Logon:
                    return "A";

                case MessageType.Logout:
                    return "5";

                case MessageType.Reject:
                    return "3";

                case MessageType.ResendRequest:
                    return "2";

                case MessageType.SequenceReset:
                    return "4";

                case MessageType.TestRequest:
                    return "1";

                case MessageType.NewOrder:
                    return "D";

                case MessageType.OrderStatusRequest:
                    return "H";

                case MessageType.OrderCancel:
                    return "F";

                case MessageType.OrderMassCancel:
                    return "q";

                case MessageType.OrderReplace:
                    return "G";

                case MessageType.MarketDataRequest:
                    return "V";

                default:
                    return "0";
            }
        }
    }

}

