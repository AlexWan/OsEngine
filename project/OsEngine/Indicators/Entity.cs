using System.Collections.Generic;


namespace OsEngine.Indicators
{
    public class Entity
    {

        public static readonly List<string> CandlePointsArray = new List<string>
            {"Open","High","Low","Close","Median","Typical"};

        /// <summary>
        /// what price of candle taken when building
        /// какая цена свечи берётся при построении
        /// </summary>
        public enum CandlePointType
        {
            /// <summary>
            /// Open
            /// открытие
            /// </summary>
            Open,

            /// <summary>
            /// High
            /// максимум
            /// </summary>
            High,

            /// <summary>
            /// Low
            /// минимум
            /// </summary>
            Low,

            /// <summary>
            /// Close
            /// закрытие
            /// </summary>
            Close,

            /// <summary>
            /// Median. (High + Low) / 2
            /// медиана. (High + Low) / 2
            /// </summary>
            Median,

            /// <summary>
            /// Typical price (High + Low + Close) / 3
            /// типичная цена (High + Low + Close) / 3
            /// </summary>
            Typical
        }

    }
}