using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff
{
    public class TinkoffServerPermission:IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.Tinkoff; }
        }

        #region DataFeedPermissions

        public bool DataFeedTf1SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf2SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf5SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf10SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf15SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf20SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf30SecondCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf1MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf2MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf5MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf10MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf15MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf30MinuteCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf1HourCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf2HourCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTf4HourCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTfTickCanLoad
        {
            get { return false; }
        }
        public bool DataFeedTfMarketDepthCanLoad
        {
            get { return false; }
        }

        #endregion

        #region Trade permission

        public bool IsTradeServer
        {
            get { return true; }
        }

        #endregion
    }
}
