using OsEngine.Market.Servers.FixProtocolEntities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OsEngine.Market.Servers.Lmax.LmaxEntity
{
    /// <summary>
	/// parse message from the exchange
    /// парсит сообщения от биржи
    /// </summary>
    public class FixMessageParser
    {
        private readonly object _parserLocker = new object();

        public List<FixEntity> ParseMessage(string message)
        {
            try
            {
                lock (_parserLocker)
                {
                    var stringFields = message.Split('\u0001');

                    List<FixEntity> fixEntities = new List<FixEntity>();

                    for (int i = 0; i < stringFields.Length - 1; i++)
                    {
                        var keyValue = stringFields[i].Split('=');

                        if (keyValue[0] == "8")
                        {
                            fixEntities.Add(new FixEntity());
                        }

                        var field = new Field(Convert.ToInt32(keyValue[0]), keyValue[1]);

                        fixEntities.Last().AddField(field);
                    }

                    return fixEntities;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
