using OsEngine.Entity;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class TestSpamEntity : AServerTester
    {
        private const int TimeOutMinutes = 1;

        public string TestingEntity;

        public Security TestingSecurity;

        public override void Process()
        {

            DateTime TimeOut = DateTime.Now.AddMinutes(TimeOutMinutes);

            if (TestingEntity.Equals("GetPortfolio"))
            {
                while (TimeOut > DateTime.Now)
                {
                    Thread.Sleep(10);

                    try
                    {
                        Server.ServerRealization.GetPortfolios();
                    }
                    catch (Exception exeption)
                    {
                        SetNewError(exeption.Message);
                    }
                }
            }

            if (TestingEntity.Equals("GetCandlesData"))
            {
                while (TimeOut > DateTime.Now)
                {
                    Thread.Sleep(10);

                    try
                    {
                        Server.ServerRealization.GetCandleDataToSecurity(TestingSecurity, new TimeFrameBuilder() { TimeFrame = TimeFrame.Min1 }, DateTime.Now.AddHours(-24), DateTime.Now.AddHours(-12), DateTime.UtcNow);
                    }
                    catch (Exception exeption)
                    {
                        SetNewError(exeption.Message);
                    }
                }
            }

            TestEnded();
        }

        public void ErrorMessageServer(string message, LogMessageType logMessageType)
        {
            if (logMessageType == LogMessageType.Error)
            {
                SetNewError(message);
            }
        }
    }
}
