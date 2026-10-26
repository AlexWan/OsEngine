/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OsEngine.IndicatorMarketDepth
{
    /// <summary>
    /// Фабрика стаканных индикаторов. Пока только прекомпилированные (внутри сборки).
    /// Кастомные скрипты из Custom будут добавлены позже.
    /// </summary>
    public static class MarketDepthIndicatorsFactory
    {
        public static readonly Dictionary<string, Type> Indicators = GetIndicatorTypes();

        private static Dictionary<string, Type> GetIndicatorTypes()
        {
            Assembly assembly = typeof(AMarketDepth).Assembly;
            Dictionary<string, Type> indicators = new Dictionary<string, Type>();

            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsPublic && !type.IsAbstract && typeof(AMarketDepth).IsAssignableFrom(type))
                {
                    indicators[type.Name] = type;
                }
            }

            return indicators;
        }

        public static List<string> GetIndicatorsNames()
        {
            List<string> names = new List<string>(Indicators.Keys);
            names.Sort();
            return names;
        }

        public static AMarketDepth CreateByName(string nameClass, string name, bool canDelete, StartProgram startProgram = StartProgram.IsOsTrader)
        {
            AMarketDepth indicator = null;

            if (Indicators.TryGetValue(nameClass, out Type type))
            {
                indicator = (AMarketDepth)Activator.CreateInstance(type);
            }

            if (indicator != null)
            {
                indicator.Init(name, startProgram);
                indicator.CanDelete = canDelete;
            }

            return indicator;
        }

        public static string GetIndicatorDescription(string name)
        {
            try
            {
                AMarketDepth indicator = CreateByName(name, "", false, StartProgram.IsOsOptimizer);

                if (indicator != null)
                {
                    string description = indicator.Description;
                    indicator.Delete();
                    return description;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return string.Empty;
        }

        public static MarketDepthIndicatorDataSource GetIndicatorDataSource(string name)
        {
            try
            {
                AMarketDepth indicator = CreateByName(name, "", false, StartProgram.IsOsOptimizer);

                if (indicator != null)
                {
                    MarketDepthIndicatorDataSource source = indicator.DataSource;
                    indicator.Delete();
                    return source;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return MarketDepthIndicatorDataSource.MarketDepth;
        }
    }
}
