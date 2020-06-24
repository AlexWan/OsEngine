namespace OsEngine.Market.Servers
{
    public interface IServerPermission
    {
        ServerType ServerType { get; }

        #region Data Feed Permissions

        bool DataFeedTf1SecondCanLoad { get; }

        bool DataFeedTf2SecondCanLoad { get; }

        bool DataFeedTf5SecondCanLoad { get; }

        bool DataFeedTf10SecondCanLoad { get; }

        bool DataFeedTf15SecondCanLoad { get; }

        bool DataFeedTf20SecondCanLoad { get; }

        bool DataFeedTf30SecondCanLoad { get; }

        bool DataFeedTf1MinuteCanLoad { get; }

        bool DataFeedTf2MinuteCanLoad { get; }

        bool DataFeedTf5MinuteCanLoad { get; }

        bool DataFeedTf10MinuteCanLoad { get; }

        bool DataFeedTf15MinuteCanLoad { get; }

        bool DataFeedTf30MinuteCanLoad { get; }

        bool DataFeedTf1HourCanLoad { get; }

        bool DataFeedTf2HourCanLoad { get; }

        bool DataFeedTf4HourCanLoad { get; }

        bool DataFeedTfTickCanLoad { get; }

        bool DataFeedTfMarketDepthCanLoad { get; }

        #endregion

        #region Trade Permissions

        bool IsTradeServer { get; }



        #endregion


    }
}
