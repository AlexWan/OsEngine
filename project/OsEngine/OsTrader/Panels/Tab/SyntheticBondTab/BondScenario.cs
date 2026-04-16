/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Iceberg;
using System;
using System.IO;

namespace OsEngine.OsTrader.Panels.Tab.SyntheticBondTab
{
    public class BondScenario
    {
        #region Constructor

        public string UniqueName;

        public string ScriptName;

        public int ScenarioNumber;

        public bool IsActiveScenario = false;

        private StartProgram StartProgram;

        public BondScenario(string nameScript, string uniqueName, int scenarioNumber, StartProgram startProgram)
        {
            UniqueName = uniqueName;
            ScriptName = nameScript;
            ScenarioNumber = scenarioNumber;
            StartProgram = startProgram;

            LoadBondScenario();

            if (NonTradePeriods == null)
            {
                NonTradePeriods = new NonTradePeriods(UniqueName);
            }

            if (ArbitrationIceberg == null)
            {
                ArbitrationIceberg = new ArbitrationIceberg(UniqueName + "ArbitrationIceberg", StartProgram);
                ArbitrationIceberg.NonTradePeriods = NonTradePeriods;
            }

            ArbitrationIceberg.AllPositionsFilledEvent += OnAllPositionsFilled;
            ArbitrationIceberg.AllPositionsClosedEvent += OnAllPositionsClosed;
        }

        private void LoadBondScenario()
        {
            if (!File.Exists(@"Engine\" + UniqueName + @"ToLoad.txt"))
            {
                return;
            }

            using (StreamReader reader = new StreamReader(@"Engine\" + UniqueName + @"ToLoad.txt"))
            {
                MaxSpread = reader.ReadLine().ToDecimal();
                MinSpread = reader.ReadLine().ToDecimal();
                IsActiveScenario = Convert.ToBoolean(reader.ReadLine());
                ScriptName = reader.ReadLine();

                ArbitrationIceberg = new ArbitrationIceberg(reader.ReadLine(), StartProgram);
                NonTradePeriods = new NonTradePeriods(reader.ReadLine());
            }
        }

        public void Save()
        {
            using (StreamWriter writer = new StreamWriter(@"Engine\" + UniqueName + @"ToLoad.txt", false))
            {
                writer.WriteLine(MaxSpread.ToString());
                writer.WriteLine(MinSpread.ToString());
                writer.WriteLine(IsActiveScenario.ToString());
                writer.WriteLine(ScriptName.ToString());
                writer.WriteLine(ArbitrationIceberg.UniqueName.ToString());
                writer.WriteLine(NonTradePeriods.NameUnique.ToString());

                ArbitrationIceberg.Save();
                NonTradePeriods.Save();
            }
        }

        /// <summary>
        /// Deletes this script
        /// | Удаляет данный сценарий
        /// </summary>
        public void Delete()
        {
            ArbitrationIceberg?.Delete();
            NonTradePeriods?.Delete();

            if (File.Exists(@"Engine\" + UniqueName + @"ToLoad.txt"))
            {
                File.Delete(@"Engine\" + UniqueName + @"ToLoad.txt");
            }
        }

        public void Clear()
        {
            try
            {
                ArbitrationIceberg.Clear();
            }
            catch
            {
                // ignore
            }
        }

        public bool IsReadyToTrade()
        {
            try
            {
                if (ArbitrationIceberg == null)
                    return false;

                return ArbitrationIceberg.CheckTradingReady();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        #endregion

        #region Public fields

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
            ScenarioFilledEvent?.Invoke(UniqueName);
        }

        private void OnAllPositionsClosed()
        {
            ScenarioClosedEvent?.Invoke(UniqueName);
        }

        #endregion
    }
}
