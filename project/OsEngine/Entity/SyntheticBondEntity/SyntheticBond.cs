/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Tab.SyntheticBondTab;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.Entity.SyntheticBondEntity
{
    public class SyntheticBond
    {
        public string UniqueName;

        public int UniqueNumber;

        public SyntheticBond(string uniqueName, int uniqueNumber, StartProgram startProgram)
        {
            UniqueName = uniqueName;
            UniqueNumber = uniqueNumber;
            StartProgram = startProgram;

            LoadSyntheticBond();

            if (PatternBaseTab == null)
            {
                string patternBaseTabName = UniqueName + "PatternBase";
                PatternBaseTab = new BotTabSimple(patternBaseTabName, StartProgram);
            }

            if (PatternFuturesTab == null)
            {
                string patternFuturesTabName = UniqueName + "PatternFutures";
                PatternFuturesTab = new BotTabSimple(patternFuturesTabName, StartProgram);
            }

            if (ActiveScenarios.Count == 0)
            {
                BondScenario defaultScenario = new BondScenario("Script 1", UniqueName + "BondScenario" + 1, scenarioNumber: 1, StartProgram);

                defaultScenario.IsActiveScenario = true;

                ActiveScenarios.Add(defaultScenario);
                SelectedScenario = defaultScenario;
            }

            if (SelectedScenario == null)
            {
                SelectedScenario = ActiveScenarios[0];
            }
        }

        private void LoadSyntheticBond()
        {
            if (!File.Exists(@"Engine\" + UniqueName + @"SyntheticBondToLoad.txt"))
            {
                return;
            }

            using (StreamReader reader = new StreamReader(@"Engine\" + UniqueName + @"SyntheticBondToLoad.txt"))
            {
                BaseMultiplicator = Convert.ToInt32(reader.ReadLine());
                FuturesMultiplicator = Convert.ToInt32(reader.ReadLine());
                BaseUseRationing = Convert.ToBoolean(reader.ReadLine());
                FuturesUseRationing = Convert.ToBoolean(reader.ReadLine());

                string baseRationingMode = reader.ReadLine();
                if (baseRationingMode == "Division") BaseRationingMode = RationingMode.Division;
                else if (baseRationingMode == "Multiplication") BaseRationingMode = RationingMode.Multiplication;
                else if (baseRationingMode == "Difference") BaseRationingMode = RationingMode.Difference;
                else if (baseRationingMode == "Addition") BaseRationingMode = RationingMode.Addition;

                string futuresRationingMode = reader.ReadLine();
                if (futuresRationingMode == "Division") FuturesRationingMode = RationingMode.Division;
                else if (futuresRationingMode == "Multiplication") FuturesRationingMode = RationingMode.Multiplication;
                else if (futuresRationingMode == "Difference") FuturesRationingMode = RationingMode.Difference;
                else if (futuresRationingMode == "Addition") FuturesRationingMode = RationingMode.Addition;

                string baseRationingSecurity = reader.ReadLine();
                if (baseRationingSecurity == "None")
                {
                    BaseRationingSecurity = null;
                }
                else
                {
                    BaseRationingSecurity = new BotTabSimple(baseRationingSecurity, StartProgram);
                    BaseRationingSecurity.SecuritySubscribeEvent += SecuritySubscribe;
                }

                string futuresRationingSecurity = reader.ReadLine();
                if (futuresRationingSecurity == "None")
                {
                    FuturesRationingSecurity = null;
                }
                else
                {
                    FuturesRationingSecurity = new BotTabSimple(futuresRationingSecurity, StartProgram);
                    FuturesRationingSecurity.SecuritySubscribeEvent += SecuritySubscribe;
                }

                BaseTimeOffset = Convert.ToInt32(reader.ReadLine());
                FuturesTimeOffset = Convert.ToInt32(reader.ReadLine());
                BaseTimeOffsetRationing = Convert.ToInt32(reader.ReadLine());
                FuturesTimeOffsetRationing = Convert.ToInt32(reader.ReadLine());

                CointegrationBuilder = new CointegrationBuilder();
                CointegrationBuilder.CointegrationLookBack = Convert.ToInt32(reader.ReadLine());
                CointegrationBuilder.CointegrationDeviation = reader.ReadLine().ToDecimal();
                SeparationLength = reader.ReadLine().ToDecimal();

                string rationingMainMode = reader.ReadLine();
                if (rationingMainMode == "Division") MainRationingMode = RationingMode.Division;
                else if (rationingMainMode == "Multiplication") MainRationingMode = RationingMode.Multiplication;
                else if (rationingMainMode == "Difference") MainRationingMode = RationingMode.Difference;
                else if (rationingMainMode == "Addition") MainRationingMode = RationingMode.Addition;

                string patternBaseTabName = reader.ReadLine();
                if (patternBaseTabName != "None")
                    _patternBaseTab = new BotTabSimple(patternBaseTabName, StartProgram);

                string patternFuturesTabName = reader.ReadLine();
                if (patternFuturesTabName != "None")
                    _patternFuturesTab = new BotTabSimple(patternFuturesTabName, StartProgram);

                int activeScenariosCount = Convert.ToInt32(reader.ReadLine());

                for (int i = 0; i < activeScenariosCount; i++)
                {
                    string scriptName = reader.ReadLine();
                    string uniqueName = reader.ReadLine();
                    int scenarioNumber = Convert.ToInt32(reader.ReadLine());

                    BondScenario bondScenario = new BondScenario(reader.ReadLine(), uniqueName, scenarioNumber, StartProgram);

                    if (bondScenario.IsActiveScenario)
                    {
                        SelectedScenario = bondScenario;
                    }

                    ActiveScenarios.Add(bondScenario);
                }

                int deletedScenariosCount = Convert.ToInt32(reader.ReadLine());

                for (int i = 0; i < deletedScenariosCount; i++)
                {
                    string scriptName = reader.ReadLine();
                    string uniqueName = reader.ReadLine();
                    int scenarioNumber = Convert.ToInt32(reader.ReadLine());

                    BondScenario bondScenario = new BondScenario(reader.ReadLine(), uniqueName, scenarioNumber, StartProgram);

                    DeletedScenarios.Add(bondScenario);
                }
            }
        }

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + UniqueName + @"SyntheticBondToLoad.txt", false))
                {
                    writer.WriteLine(BaseMultiplicator.ToString());
                    writer.WriteLine(FuturesMultiplicator.ToString());
                    writer.WriteLine(BaseUseRationing.ToString());
                    writer.WriteLine(FuturesUseRationing.ToString());
                    writer.WriteLine(BaseRationingMode.ToString());
                    writer.WriteLine(FuturesRationingMode.ToString());

                    writer.WriteLine(BaseRationingSecurity == null
                        ? "None"
                        : BaseRationingSecurity.TabName);

                    writer.WriteLine(FuturesRationingSecurity == null
                        ? "None"
                        : FuturesRationingSecurity.TabName);

                    writer.WriteLine(BaseTimeOffset.ToString());
                    writer.WriteLine(FuturesTimeOffset.ToString());
                    writer.WriteLine(BaseTimeOffsetRationing.ToString());
                    writer.WriteLine(FuturesTimeOffsetRationing.ToString());

                    writer.WriteLine(CointegrationBuilder.CointegrationLookBack.ToString());
                    writer.WriteLine(CointegrationBuilder.CointegrationDeviation.ToString());
                    writer.WriteLine(SeparationLength.ToString());
                    writer.WriteLine(MainRationingMode.ToString());

                    if (_patternBaseTab != null &&
                      !string.IsNullOrEmpty(_patternBaseTab.TabName))
                        writer.WriteLine(_patternBaseTab.TabName);
                    else writer.WriteLine("None");

                    if (_patternFuturesTab != null &&
                      !string.IsNullOrEmpty(_patternFuturesTab.TabName))
                        writer.WriteLine(_patternFuturesTab.TabName);
                    else writer.WriteLine("None");

                    writer.WriteLine(ActiveScenarios.Count.ToString());

                    for (int i = 0; i < ActiveScenarios.Count; i++)
                    {
                        BondScenario activeScenario = ActiveScenarios[i];

                        writer.WriteLine(activeScenario.ScriptName);
                        writer.WriteLine(activeScenario.UniqueName);
                        writer.WriteLine(activeScenario.ScenarioNumber);
                        activeScenario.Save();
                    }

                    writer.WriteLine(DeletedScenarios.Count.ToString());

                    for (int i = 0; i < DeletedScenarios.Count; i++)
                    {
                        BondScenario deletedScenario = DeletedScenarios[i];

                        writer.WriteLine(deletedScenario.ScriptName);
                        writer.WriteLine(deletedScenario.UniqueName);
                        writer.WriteLine(deletedScenario.ScenarioNumber);
                        deletedScenario.Save();
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Clear()
        {
            try
            {
                for (int i = 0; i < ActiveScenarios.Count; i++)
                {
                    BondScenario scenario = ActiveScenarios[i];
                    scenario.Clear();
                }

                if (BaseRationingSecurity != null)
                    BaseRationingSecurity.Clear();

                if (FuturesRationingSecurity != null)
                    FuturesRationingSecurity.Clear();
            }
            catch
            {
                // ignore
            }
        }

        public void Delete()
        {
            try
            {
                if (_patternBaseTab != null)
                {
                    _patternBaseTab.Delete();
                    _patternBaseTab = null;
                }

                if (_patternFuturesTab != null)
                {
                    _patternFuturesTab.Delete();
                    _patternFuturesTab = null;
                }

                for (int i = 0; i < ActiveScenarios.Count; i++)
                {
                    BondScenario scenario = ActiveScenarios[i];
                    scenario.Delete();
                }

                ActiveScenarios.Clear();

                if (File.Exists(@"Engine\" + UniqueName + @"SyntheticBondToLoad.txt"))
                {
                    File.Delete(@"Engine\" + UniqueName + @"SyntheticBondToLoad.txt");
                }
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
                bool isReadyToTrade = true;

                if (ActiveScenarios.Count == 0)
                    return false;

                for (int i = 0; i < ActiveScenarios.Count; i++)
                {
                    BondScenario scenario = ActiveScenarios[i];

                    if (scenario.IsReadyToTrade() == false)
                        return false;
                }

                return isReadyToTrade;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void UpdateFuturesSecurity()
        {
            try
            {
                if (_patternFuturesTab != null)
                {
                    _patternFuturesTab.Delete();
                    _patternFuturesTab = null;
                }

                _patternFuturesTab = new BotTabSimple(UniqueName + "PatternFuturesTab", StartProgram);
                _patternFuturesTab.DialogClosed += OnFuturesSecurityDialogClosed;
                _patternFuturesTab.SecuritySubscribeEvent += SecuritySubscribe;
                _patternFuturesTab.ShowConnectorDialog();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SecuritySubscribe(Security security)
        {
            Save();
            SecuritySubscribeEvent?.Invoke(security);
        }

        private void OnFuturesSecurityDialogClosed()
        {
            _patternFuturesTab.DialogClosed -= OnFuturesSecurityDialogClosed;
            PropagateSecurity(ref _patternFuturesTab, isBase: false);
        }

        private void PropagateSecurity(ref BotTabSimple patternTab, bool isBase, BondScenario scenario = null)
        {
            if (patternTab == null ||
               (patternTab != null && patternTab.Connector == null))
            {
                return;
            }

            ConnectorCandles sourceConnector = patternTab.Connector;

            if (scenario != null)
            {
                BotTabSimple targetTab = null;

                if (isBase)
                    targetTab = scenario.ArbitrationIceberg.MainLegs[0].BotTab;
                else
                    targetTab = scenario.ArbitrationIceberg.SecondaryLegs[0].BotTab;

                UpdateConnectorCandles(targetTab.Connector, sourceConnector);
                return;
            }

            for (int i2 = 0; i2 < ActiveScenarios.Count; i2++)
            {
                BondScenario currentScenario = ActiveScenarios[i2];

                BotTabSimple targetTab = null;

                if (isBase)
                    targetTab = currentScenario.ArbitrationIceberg.MainLegs[0].BotTab;
                else
                    targetTab = currentScenario.ArbitrationIceberg.SecondaryLegs[0].BotTab;

                bool needUpdate = true;

                if (targetTab == null ||
                       (targetTab != null && targetTab.Connector == null))
                    needUpdate = false;
                else if (targetTab.Connector.SecurityName != _patternBaseTab.Connector.SecurityName)
                    needUpdate = true;

                if (needUpdate)
                {
                    UpdateConnectorCandles(targetTab.Connector, sourceConnector);
                }
            }
        }

        private void UpdateConnectorCandles(ConnectorCandles targetConnector, ConnectorCandles sourceConnector)
        {
            targetConnector.ServerFullName = sourceConnector.ServerFullName;
            targetConnector.PortfolioName = sourceConnector.PortfolioName;
            targetConnector.EmulatorIsOn = sourceConnector.EmulatorIsOn;
            targetConnector.SecurityName = sourceConnector.SecurityName;
            targetConnector.SecurityClass = sourceConnector.SecurityClass;
            targetConnector.CandleMarketDataType = sourceConnector.CandleMarketDataType;
            targetConnector.CommissionType = sourceConnector.CommissionType;
            targetConnector.CommissionValue = sourceConnector.CommissionValue;
            targetConnector.CandleCreateMethodType = sourceConnector.CandleCreateMethodType;
            targetConnector.TimeFrame = sourceConnector.TimeFrame;
            targetConnector.SaveTradesInCandles = sourceConnector.SaveTradesInCandles;
            targetConnector.ServerType = sourceConnector.ServerType;

            targetConnector.TimeFrameBuilder.Save();
            targetConnector.Save();
            targetConnector.ReconnectHard();
        }

        public BondScenario CreateNewScenario(string scenarioName)
        {
            try
            {
                int number = GetAvailableScenarioNumber();

                BondScenario scenario = new BondScenario(scenarioName, UniqueName + "BondScenario" + number, number, StartProgram);
                PropagateSecurity(ref _patternBaseTab, isBase: true, scenario);
                PropagateSecurity(ref _patternFuturesTab, isBase: false, scenario);

                ActiveScenarios.Add(scenario);
                return scenario;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public int GetAvailableScenarioNumber()
        {
            int scenarioNumber = 1;

            if (ActiveScenarios.Count > 0)
            {
                for (int i = 0; i < ActiveScenarios.Count; i++)
                {
                    if (ActiveScenarios[i].ScenarioNumber == scenarioNumber)
                    {
                        scenarioNumber++;
                        continue;
                    }

                    for (int i2 = 0; i2 < DeletedScenarios.Count; i2++)
                    {
                        if (DeletedScenarios[i2].ScenarioNumber == scenarioNumber)
                        {
                            scenarioNumber++;
                            continue;
                        }
                    }
                }
            }
            else
            {
                for (int i2 = 0; i2 < DeletedScenarios.Count; i2++)
                {
                    if (DeletedScenarios[i2].ScenarioNumber == scenarioNumber)
                    {
                        scenarioNumber++;
                        continue;
                    }
                }
            }

            return scenarioNumber;
        }

        /// <summary>
        /// Trading scenarios for this bond pair. Each scenario has independent trading parameters.
        /// | Торговые сценарии для данной пары. Каждый сценарий имеет независимые торговые параметры.
        /// </summary>
        public List<BondScenario> ActiveScenarios = new List<BondScenario>();

        /// <summary>
        /// Scripts that have been deleted | Сценарии, которые были удалены
        /// </summary>
        public List<BondScenario> DeletedScenarios = new List<BondScenario>();

        /// <summary>
        /// The price multiplier in a pair for futures. The default is 1 | Ценовой мультипликатор в паре для фьючерса. По умолчанию значение 1
        /// </summary>
        public decimal FuturesMultiplicator = 1;

        /// <summary>
        /// The price multiplier in a pair for base. The default is 1 | Ценовой мультипликатор в паре для базы. По умолчанию значение 1
        /// </summary>
        public decimal BaseMultiplicator = 1;

        /// <summary>
        /// Enable or disable rationing for the third security for futures | Включить или выключить нормирование по третьему инструменту для фьючерса
        /// </summary>
        public bool FuturesUseRationing;

        /// <summary>
        /// Enable or disable rationing for the third security for futures | Включить или выключить нормирование по третьему инструменту для базы
        /// </summary>
        public bool BaseUseRationing;

        /// <summary>
        /// The type of rationing for futures. Division, multiplication, difference, addition. | Тип нормирования для фьючерса. Деление, умножение, разница, сложение.
        /// </summary>
        public RationingMode FuturesRationingMode;

        /// <summary>
        /// The type of rationing for base. Division, multiplication, difference, addition. | Тип нормирования для базы. Деление, умножение, разница, сложение.
        /// </summary>
        public RationingMode BaseRationingMode;

        /// <summary>
        /// The main type of rationing. Division, multiplication, difference, addition. | Основной тип нормирования. Деление, умножение, разница, сложение.
        /// </summary>
        public RationingMode MainRationingMode;

        /// <summary>
        /// The security used for rationing for futures | Инструмент для нормирования для фьючерса
        /// </summary>
        public BotTabSimple FuturesRationingSecurity;

        /// <summary>
        /// The security used for rationing for base | Инструмент для нормирования для базы
        /// </summary>
        public BotTabSimple BaseRationingSecurity;

        /// <summary>
        /// The size of the tool offset in hours for futures. It can be either + or - | Размер смещения инструмента в часах для фьючерса. Это может быть значение + или -
        /// </summary>
        public int BaseTimeOffset;

        /// <summary>
        /// The size of the tool offset in hours for futures. It can be either + or - | Размер смещения инструмента в часах для фьючерса. Это может быть значение + или -
        /// </summary>
        public int FuturesTimeOffset;

        /// <summary>
        /// The size of the tool offset in hours for futures. It can be either + or - | Размер смещения инструмента в часах для фьючерса. Это может быть значение + или -
        /// </summary>
        public int FuturesTimeOffsetRationing;

        /// <summary>
        /// The size of the tool offset in hours for base. It can be either + or - | Размер смещения инструмента в часах для базы. Это может быть значение + или -
        /// </summary>
        public int BaseTimeOffsetRationing;

        /// <summary>
        /// The current minimum balance between the base and the futures | Текущий минимальный остаток между базой и фьючерсом
        /// </summary>
        public CointegrationBuilder CointegrationBuilder;

        /// <summary>
        /// The current percentage breakdown between the base and the futures | Текущая раздвижка между базой и фьючерсом в процентах
        /// </summary>
        public List<PairIndicatorValue> PercentSeparationCandles = new List<PairIndicatorValue>();

        /// <summary>
        /// The current sliding between the base and the futures in absolute terms | Текущая раздвижка между базой и фьючерсом в абсолюте
        /// </summary>
        public List<PairIndicatorValue> AbsoluteSeparationCandles = new List<PairIndicatorValue>();

        /// <summary>
        /// History separation changes. Amount of data | История изменения раздвижки. Количество данных
        /// </summary>
        public decimal SeparationLength = 50;

        /// <summary>
        /// Days before the expiration of the futures contract. If the value is negative, then the validity period is infinite | Дней до истечения срока действия фьючерса. Если отрицательное значение, то срок действия бесконечный
        /// </summary>
        public int DaysBeforeExpiration
        {
            get
            {
                if (_patternFuturesTab == null) return -1;
                else if (_patternFuturesTab.Connector == null) return -1;
                else if (_patternFuturesTab.Connector.Security == null) return -1;

                if (_patternFuturesTab.Security.Expiration != DateTime.MinValue && _patternFuturesTab.Security.Expiration != DateTime.UnixEpoch)
                {
                    return (_patternFuturesTab.Security.Expiration - _patternFuturesTab.TimeServerCurrent).Days;
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Profit per day | Профит в день
        /// </summary>
        public decimal ProfitPerDay = 0;

        public BotTabSimple PatternBaseTab
        {
            get { return _patternBaseTab; }
            set
            {
                if (_patternBaseTab != null)
                {
                    _patternBaseTab.Delete();
                    _patternBaseTab = null;
                }

                _patternBaseTab = value;
            }
        }

        public BotTabSimple PatternFuturesTab
        {
            get { return _patternFuturesTab; }
            set
            {
                if (_patternFuturesTab != null)
                {
                    _patternFuturesTab.Delete();
                    _patternFuturesTab = null;
                }

                _patternFuturesTab = value;
            }
        }

        public BondScenario SelectedScenario;

        #region Private fields

        private BotTabSimple _patternFuturesTab;

        private BotTabSimple _patternBaseTab;

        private StartProgram StartProgram;

        #endregion

        #region Events

        public event Action<Security> SecuritySubscribeEvent;

        #endregion
    }

    public enum RationingMode
    {
        Difference,

        Division,

        Multiplication,

        Addition
    }

    public enum SynteticBondOrderPosition
    {
        Ask,

        Bid,

        Middle
    }
}
