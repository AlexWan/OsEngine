using System;
using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class Trailer
    {
        private string _Message;

        public Trailer(string message)
        {
            _Message = message;
        }

        public override string ToString()
        {
            int sumChar = 0;

            for (int i = 0; i < _Message.Length; i++)
            {
                sumChar += (int)_Message[i];
            }

            string checksum = Convert.ToString(sumChar % 256).PadLeft(3, '0');

            StringBuilder sb = new StringBuilder();

            sb.Append("10=").Append(checksum).Append('\u0001');

            return sb.ToString();
        }
    }
}
