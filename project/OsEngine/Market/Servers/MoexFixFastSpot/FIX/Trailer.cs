using System;

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
            return $"10={checksum}\u0001";
        }
    }
}
