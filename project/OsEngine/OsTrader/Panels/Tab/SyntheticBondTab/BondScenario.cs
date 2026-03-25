/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.OsTrader.Iceberg;
using System;
using System.IO;

namespace OsEngine.OsTrader.Panels.Tab.SyntheticBondTab
{
    public class BondScenario
    {
        #region Constructor

        public BondScenario(string futuresTabName, string scenarioName)
        {
            Name = scenarioName;

            string icebergName = futuresTabName + "_" + scenarioName;

            bool fileExists = File.Exists(@"Engine\ArbitrationIceberg\" + icebergName + "ArbitrationIcebergParameters.txt");

            ArbitrationIceberg = new ArbitrationIceberg(icebergName);
            ArbitrationIceberg.AllPositionsFilledEvent += OnAllPositionsFilled;
            ArbitrationIceberg.AllPositionsClosedEvent += OnAllPositionsClosed;

            if (!fileExists)
            {
                ArbitrationIceberg.Pause();
            }

            NonTradePeriods = new NonTradePeriods(icebergName);

            ArbitrationIceberg.NonTradePeriods = NonTradePeriods;
        }

        #endregion

        #region Public fields

        /// <summary>
        /// Scenario name. | Имя сценария.
        /// </summary>
        public string Name;

        /// <summary>
        /// The trading module for this scenario. | Торговый модуль данного сценария.
        /// </summary>
        public ArbitrationIceberg ArbitrationIceberg;

        /// <summary>
        /// The maximum spread at which the bond opens. | Максимальный спред, при котором облигация открывается.
        /// </summary>
        public decimal MaxSpread;

        /// <summary>
        /// The minimum spread at which the position closes. | Минимальный спред, при котором позиция закрывается.
        /// </summary>
        public decimal MinSpread;

        /// <summary>
        /// Non-trading periods for this scenario. | Неторговые периоды данного сценария.
        /// </summary>
        public NonTradePeriods NonTradePeriods;

        #endregion

        #region Events

        /// <summary>
        /// Fires when all legs have been filled to target volume.
        /// | Вызывается, когда все ноги набрали целевой объём.
        /// </summary>
        public Action<string> ScenarioFilledEvent;

        /// <summary>
        /// Fires when all legs have been closed.
        /// | Вызывается, когда все ноги закрыты.
        /// </summary>
        public Action<string> ScenarioClosedEvent;

        private void OnAllPositionsFilled()
        {
            ScenarioFilledEvent?.Invoke(Name);
        }

        private void OnAllPositionsClosed()
        {
            ScenarioClosedEvent?.Invoke(Name);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Assigns BotTabs to this scenario's ArbitrationIceberg.
        /// | Назначает BotTab-ы ArbitrationIceberg данного сценария.
        /// </summary>
        public void SetBotTabs(BotTabSimple baseBotTab, BotTabSimple futuresBotTab)
        {
            ArbitrationIceberg.SetBotTabs(baseBotTab, futuresBotTab);
        }

        /// <summary>
        /// Deletes all persistent files for this scenario.
        /// | Удаляет все файлы данного сценария.
        /// </summary>
        public void Delete()
        {
            ArbitrationIceberg?.Delete();
            NonTradePeriods?.Delete();
        }

        #endregion
    }
}
