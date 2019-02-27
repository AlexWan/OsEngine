using System;
using System.Collections.Generic;
using System.Text;
using OsEngine.Market.Servers.Lmax;

namespace OsEngine.Market.Servers.FixProtocolEntities
{
    public class FixMessage
    {
        private const string Soh = "\u0001";

        private readonly List<Field> _fields = new List<Field>();

        /// <summary>
        /// when creating a message, standard fields are set immediately
        /// при создании сообщения сразу задаются стандартные поля
        /// </summary>
        /// <param name="msgSeqNum">message sequence number / порядковый номер сообщения</param>
        /// <param name="type">message type / тип сообщения</param>
        /// <param name="isTradingMsg">flag indicating for which session this message is generated / флаг указывающий для какой сессии это сообщение создается, true - trading, false - marketData </param>
        /// <param name="headerSettings">set of standard parameters / набор стандартных параметров</param>
        public FixMessage(long msgSeqNum, string type, bool isTradingMsg, StandartHeaderSettings headerSettings)
        {

            _fields.Add(new Field((int)Tags.BeginString, headerSettings.BeginString));
            _fields.Add(new Field((int)Tags.BodyLength, "0"));
            _fields.Add(new Field((int)Tags.MsgType, type));
            _fields.Add(new Field((int)Tags.SenderCompID, headerSettings.SenderCompId));
            _fields.Add(new Field((int)Tags.TargetCompID, isTradingMsg ? headerSettings.TargetCompIdTrd : headerSettings.TargetCompIdMd));
            _fields.Add(new Field((int)Tags.MsgSeqNum, msgSeqNum.ToString()));
            _fields.Add(new Field((int)Tags.SendingTime, DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss")));
        }

        /// <summary>
        /// add body to the message
        /// добавить тело к сообщению
        /// </summary>
        public void AddBody(List<Field> body)
        {
            _fields.AddRange(body);
        }

        /// <summary>
        /// take string in the FIX format
        /// взять строку в формате FIX
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var field in _fields)
            {
                if (field.Tag == 8 || field.Tag == 9)
                {
                    continue;
                }
                sb.Append(field.Tag + "=" + field.Value + Soh);
            }

            _fields[1].Value = sb.Length.ToString();

            sb.Insert(0, "8=" + _fields[0].Value + Soh + "9=" + _fields[1].Value + Soh);

            int sumChar = 0;

            for (int i = 0; i < sb.Length; i++)
            {
                sumChar += sb[i];
            }

            string trailer = $"10={Convert.ToString(sumChar % 256).PadLeft(3).Replace(" ", "0")}{Soh}";

            return sb.Append(trailer).ToString();
        }
    }
}
