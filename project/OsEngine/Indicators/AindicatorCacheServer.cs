/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;

namespace OsEngine.Indicators
{
    public class AindicatorCacheServer
    {
        public static void Clear()
        {
            Cache = new List<AindicatorCache>();
        }

        private static string _cacheLocker = "_cacheLocker";

        public static bool IsOn = false;

        public static void TrySetIndicatorValuesInCache(BotPanel robot)
        {
            if(IsOn == false)
            {
                return;
            }

            List<BotTabSimple> tabs = new List<BotTabSimple>();

            for(int i = 0;i < robot.TabsSimple.Count;i++)
            {
                tabs.Add(robot.TabsSimple[i]);
            }

            for(int i = 0;i < robot.TabsScreener.Count;i++)
            {
                if(robot.TabsScreener[i].Tabs == null 
                    || robot.TabsScreener[i].Tabs.Count == 0)
                {
                    continue;
                }

                tabs.AddRange(robot.TabsScreener[i].Tabs);
            }

            for(int i = 0;i < tabs.Count;i++)
            {
                BotTabSimple currentTab = tabs[i];
                List<Candle> candles = currentTab.CandlesAll;

                if(candles == null || candles.Count == 0)
                {
                    continue;
                }

                List<IIndicator> indicators = currentTab.Indicators;

                if(indicators == null || indicators.Count == 0)
                {
                    continue;
                }

                for(int j = 0;j < indicators.Count;j++)
                {
                    IIndicator currentIndicator = indicators[j];

                    Type indType = currentIndicator.GetType();

                    if (indType.BaseType.Name != "Aindicator")
                    {
                        continue;
                    }

                    Aindicator aIndicator = ((Aindicator)currentIndicator);

                    AindicatorCache newCache = new AindicatorCache();
                    newCache.IndicatorName = currentIndicator.GetType().Name;
                    newCache.CandlesSpecification = currentTab.Connector.TimeFrameBuilder.Specification;
                    newCache.SecurityName = currentTab.Connector.SecurityName;
                    newCache.IndicatorSettingsSpecification = aIndicator.ParametersSpecification;
                    newCache.CandleStart = candles[0].TimeStart;
                    newCache.CandleEnd = candles[^1].TimeStart;

                    TrySaveCacheInArray(aIndicator, newCache);
                }
            }
        }

        private static void TrySaveCacheInArray(Aindicator aIndicator, AindicatorCache newCache)
        {
            lock (_cacheLocker)
            {
                bool isInArray = false;

                for (int i = 0; i < Cache.Count; i++)
                {
                    if (Cache[i].IndicatorName == newCache.IndicatorName
                        && Cache[i].CandlesSpecification == newCache.CandlesSpecification
                        && Cache[i].SecurityName == newCache.SecurityName
                        && Cache[i].IndicatorSettingsSpecification == newCache.IndicatorSettingsSpecification
                        && Cache[i].CandleStart == newCache.CandleStart
                        && Cache[i].CandleEnd == newCache.CandleEnd)
                    {
                        isInArray = true;
                    }
                }

                if (isInArray == true)
                {
                    return;
                }

                // индикаторы с перестраивающимися сериями не сохраняем в КЭШ. Их надо строить на лету...

                for (int i = 0; i < aIndicator.DataSeries.Count; i++)
                {
                    IndicatorDataSeries currentSeries = aIndicator.DataSeries[i];

                    if (currentSeries.CanReBuildHistoricalValues == true)
                    {
                        return;
                    }
                }

                // сохраняем новый кэш в массив

                List<IndicatorDataSeries> newDataSeries = new List<IndicatorDataSeries>();

                for (int i = 0; i < aIndicator.DataSeries.Count; i++)
                {
                    IndicatorDataSeries currentSeries = aIndicator.DataSeries[i];
                    IndicatorDataSeries newSeries = new IndicatorDataSeries(currentSeries.Color, currentSeries.Name, currentSeries.ChartPaintType, currentSeries.IsPaint);

                    for (int j = 0; j < currentSeries.Values.Count; j++)
                    {
                        newSeries.Values.Add(currentSeries.Values[j]);
                    }

                    newDataSeries.Add(newSeries);
                }

                newCache.DataSeries = newDataSeries;

                Cache.Add(newCache);
            }
        }

        public static List<AindicatorCache> Cache = new List<AindicatorCache>();

        public static AindicatorCache TryGetValuesToIndicator(Aindicator indicator, string securityName, 
            string candlesSpecification, List<Candle> candles)
        {
            if (IsOn == false)
            {
                return null;
            }

            lock (_cacheLocker)
            {
                AindicatorCache result = null;

                for (int i = 0; i < Cache.Count; i++)
                {
                    if(result != null
                        && result.CandleEnd >= Cache[i].CandleEnd)
                    {
                        continue;
                    }

                    if (Cache[i].IndicatorName == indicator.GetType().Name
                        && Cache[i].CandlesSpecification == candlesSpecification
                        && Cache[i].SecurityName == securityName
                        && Cache[i].IndicatorSettingsSpecification == indicator.ParametersSpecification
                        && Cache[i].CandleStart == candles[0].TimeStart)
                    {
                        result = Cache[i];
                    }
                }

                return result;
            }
        }
    }

    public class AindicatorCache
    {
        public string IndicatorName;

        public string IndicatorSettingsSpecification;

        public string SecurityName;

        public string CandlesSpecification;

        public DateTime CandleStart;

        public DateTime CandleEnd;

        public List<IndicatorDataSeries> DataSeries = new List<IndicatorDataSeries>();

    }
}
