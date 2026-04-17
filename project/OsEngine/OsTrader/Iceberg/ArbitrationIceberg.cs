using OsEngine.Entity;
using OsEngine.Entity.SyntheticBondEntity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace OsEngine.OsTrader.Iceberg
{
    public class ArbitrationIceberg
    {
        #region Constructor

        public ArbitrationIceberg(string name, StartProgram startProgram)
        {
            UniqueName = name;
            StartProgram = startProgram;

            LoadArbitrationIceberg();

            if (_mainLegs == null)
                _mainLegs = new List<ArbitrationLeg>();

            if (_mainLegs.Count == 0)
            {
                string baseTabName = UniqueName + "Base" + 1.ToString();
                ArbitrationLeg mainLeg = new ArbitrationLeg();
                mainLeg.BotTab = new BotTabSimple(baseTabName, StartProgram);

                _mainLegs.Add(mainLeg);
            }

            if (_secondaryLegs == null)
                _secondaryLegs = new List<ArbitrationLeg>();

            if (_secondaryLegs.Count == 0)
            {
                string futuresTabName = UniqueName + "Futures" + 1.ToString();
                ArbitrationLeg secondaryLeg = new ArbitrationLeg();
                secondaryLeg.BotTab = new BotTabSimple(futuresTabName, StartProgram);

                _secondaryLegs.Add(secondaryLeg);
            }
        }

        private void LoadArbitrationIceberg()
        {
            try
            {

                if (!File.Exists(@"Engine\" + UniqueName + @"ToLoad.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\" + UniqueName + @"ToLoad.txt"))
                {
                    string currentStatus = reader.ReadLine();
                    _currentStatus = ArbitrationStatus.Pause;

                    //if (currentStatus == "Off")
                    //    _currentStatus = ArbitrationStatus.Off;
                    //else if (currentStatus == "On")
                    //    _currentStatus = ArbitrationStatus.On;

                    string currentMode = reader.ReadLine();

                    if (currentMode == "CloseScript")
                        _currentMode = ArbitrationMode.CloseScript;
                    else if (currentMode == "CloseAllScripts")
                        _currentMode = ArbitrationMode.CloseAllScripts;
                    else if (currentMode == "OpenSellFirstBuySecond")
                        _currentMode = ArbitrationMode.OpenSellFirstBuySecond;
                    else if (currentMode == "OpenBuyFirstSellSecond")
                        _currentMode = ArbitrationMode.OpenBuyFirstSellSecond;

                    string nonTradePeriodsNameUnique = reader.ReadLine();
                    NonTradePeriods = new NonTradePeriods(nonTradePeriodsNameUnique);

                    string stopTradeOnFailOrders = reader.ReadLine();
                    if (stopTradeOnFailOrders == "True")
                        _stopTradeOnFailOrders = true;
                    else if (stopTradeOnFailOrders == "False")
                        _stopTradeOnFailOrders = false;

                    int mainLegsCount = Convert.ToInt32(reader.ReadLine());
                    int secondaryLegsCount = Convert.ToInt32(reader.ReadLine());

                    reader.ReadLine();
                    reader.ReadLine();
                    reader.ReadLine();

                    _mainLegs = new List<ArbitrationLeg>();
                    _secondaryLegs = new List<ArbitrationLeg>();

                    ReadLegInfo(mainLegsCount, reader, ref _mainLegs);
                    ReadLegInfo(secondaryLegsCount, reader, ref _secondaryLegs);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ReadLegInfo(int count, StreamReader reader, ref List<ArbitrationLeg> arbitrationLegs)
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    ArbitrationLeg leg = new ArbitrationLeg();

                    leg.AssetPortfolio = reader.ReadLine();
                    leg.EnterLifetimeOrder = Convert.ToInt32(reader.ReadLine());
                    leg.EnterOneOrderVolume = Convert.ToInt32(reader.ReadLine());
                    leg.EnterOrderFrequency = Convert.ToInt32(reader.ReadLine());

                    string enterOrderPosition = reader.ReadLine();
                    if (enterOrderPosition == "Middle")
                        leg.EnterOrderPosition = SynteticBondOrderPosition.Middle;
                    else if (enterOrderPosition == "Bid")
                        leg.EnterOrderPosition = SynteticBondOrderPosition.Bid;
                    else if (enterOrderPosition == "Ask")
                        leg.EnterOrderPosition = SynteticBondOrderPosition.Ask;

                    string enterOrderType = reader.ReadLine();
                    if (enterOrderType == "Limit")
                        leg.EnterOrderType = OrderPriceType.Limit;
                    else if (enterOrderType == "Market")
                        leg.EnterOrderType = OrderPriceType.Market;

                    leg.EnterSlippage = reader.ReadLine().ToDecimal();
                    leg.ExitLifetimeOrder = Convert.ToInt32(reader.ReadLine());
                    leg.ExitOneOrderVolume = Convert.ToInt32(reader.ReadLine());
                    leg.ExitOrderFrequency = Convert.ToInt32(reader.ReadLine());

                    string exitOrderPosition = reader.ReadLine();
                    if (exitOrderPosition == "Middle")
                        leg.ExitOrderPosition = SynteticBondOrderPosition.Middle;
                    else if (exitOrderPosition == "Bid")
                        leg.ExitOrderPosition = SynteticBondOrderPosition.Bid;
                    else if (exitOrderPosition == "Ask")
                        leg.ExitOrderPosition = SynteticBondOrderPosition.Ask;

                    string exitOrderType = reader.ReadLine();
                    if (exitOrderType == "Limit")
                        leg.ExitOrderType = OrderPriceType.Limit;
                    else if (exitOrderType == "Market")
                        leg.ExitOrderType = OrderPriceType.Market;

                    leg.ExitSlippage = reader.ReadLine().ToDecimal();

                    string volumeType = reader.ReadLine();
                    if (volumeType == "Contracts")
                    {
                        leg.VolumeType = VolumeType.Contracts;
                    }
                    else if (volumeType == "ContractCurrency")
                    {
                        leg.VolumeType = VolumeType.ContractCurrency;
                    }
                    else if (volumeType == "DepositPercent")
                    {
                        leg.VolumeType = VolumeType.DepositPercent;
                    }

                    string botName = reader.ReadLine();
                    leg.BotTab = new BotTabSimple(botName, StartProgram);

                    string currentPosition = reader.ReadLine();
                    if (currentPosition == "None")
                    {
                        leg.ArbitrationLegStatistic.CurrentPosition = null;
                    }
                    else
                    {
                        int currentPositionNumber = Convert.ToInt32(currentPosition);

                        for (int i2 = 0; leg.BotTab != null && i2 < leg.BotTab.PositionsAll.Count; i2++)
                        {
                            if (currentPositionNumber == leg.BotTab.PositionsAll[i2].Number)
                            {
                                leg.ArbitrationLegStatistic.CurrentPosition = leg.BotTab.PositionsAll[i2];
                                break;
                            }
                        }
                    }

                    string legStatus = reader.ReadLine();
                    if (legStatus == "None")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.None;
                    else if (legStatus == "Active")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.Active;
                    else if (legStatus == "Pending")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.Pending;
                    else if (legStatus == "Done")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.Done;
                    else if (legStatus == "Partial")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.Partial;
                    else if (legStatus == "Fail")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.Fail;
                    else if (legStatus == "Cancel")
                        leg.ArbitrationLegStatistic.Status = OrderStateType.Cancel;

                    leg.ArbitrationLegStatistic.OpenVolume = reader.ReadLine().ToDecimal();

                    string legSide = reader.ReadLine();
                    if (legSide == "None")
                        leg.ArbitrationLegStatistic.Side = Side.None;
                    else if (legSide == "Buy")
                        leg.ArbitrationLegStatistic.Side = Side.Buy;
                    else if (legSide == "Sell")
                        leg.ArbitrationLegStatistic.Side = Side.Sell;

                    leg.TotalVolume = reader.ReadLine().ToDecimal();

                    int enterArbitrationStepsCount = Convert.ToInt32(reader.ReadLine());
                    leg.EnterArbitrationSteps = new List<ArbitrationStep>();
                    ReadArbitrationSteps(enterArbitrationStepsCount, ref leg.EnterArbitrationSteps, reader);

                    int exitArbitrationStepsCount = Convert.ToInt32(reader.ReadLine());
                    leg.ExitArbitrationSteps = new List<ArbitrationStep>();
                    ReadArbitrationSteps(exitArbitrationStepsCount, ref leg.ExitArbitrationSteps, reader);

                    arbitrationLegs.Add(leg);

                    reader.ReadLine();
                    reader.ReadLine();
                    reader.ReadLine();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ReadArbitrationSteps(int count, ref List<ArbitrationStep> steps, StreamReader reader)
        {
            for (int i = 0; i < count; i++)
            {
                ArbitrationStep step = new ArbitrationStep();

                step.UniqStepName = reader.ReadLine();
                step.NumberStep = Convert.ToInt32(reader.ReadLine());

                string status = reader.ReadLine();

                if (status == "Active")
                    step.Status = OrderStateType.Active;
                else if (status == "Done")
                    step.Status = OrderStateType.Done;
                else if (status == "None")
                    step.Status = OrderStateType.None;

                step.LastUpdateTime = Convert.ToDateTime(reader.ReadLine());
                step.OpenVolume = reader.ReadLine().ToDecimal();
                step.StartOpenVolume = reader.ReadLine().ToDecimal();
                step.TimeActivateStep = Convert.ToDateTime(reader.ReadLine());
                step.VolumeStep = reader.ReadLine().ToDecimal();

                steps.Add(step);
            }
        }

        public void Save()
        {
            lock (_fileLock)
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + UniqueName + @"ToLoad.txt", false))
                {
                    writer.WriteLine(_currentStatus.ToString());
                    writer.WriteLine(_currentMode.ToString());
                    writer.WriteLine(_nonTradePeriods.NameUnique.ToString());
                    writer.WriteLine(_stopTradeOnFailOrders.ToString());
                    writer.WriteLine(_mainLegs.Count.ToString());
                    writer.WriteLine(_secondaryLegs.Count.ToString());

                    writer.WriteLine("/");
                    writer.WriteLine("/");
                    writer.WriteLine("/");

                    for (int i = 0; i < _mainLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _mainLegs[i];

                        writer.WriteLine(leg.AssetPortfolio.ToString());
                        writer.WriteLine(leg.EnterLifetimeOrder.ToString());
                        writer.WriteLine(leg.EnterOneOrderVolume.ToString());
                        writer.WriteLine(leg.EnterOrderFrequency.ToString());
                        writer.WriteLine(leg.EnterOrderPosition.ToString());
                        writer.WriteLine(leg.EnterOrderType.ToString());
                        writer.WriteLine(leg.EnterSlippage.ToString());
                        writer.WriteLine(leg.ExitLifetimeOrder.ToString());
                        writer.WriteLine(leg.ExitOneOrderVolume.ToString());
                        writer.WriteLine(leg.ExitOrderFrequency.ToString());
                        writer.WriteLine(leg.ExitOrderPosition.ToString());
                        writer.WriteLine(leg.ExitOrderType.ToString());
                        writer.WriteLine(leg.ExitSlippage.ToString());
                        writer.WriteLine(leg.VolumeType.ToString());
                        writer.WriteLine(leg.BotTab.TabName.ToString());

                        if (leg.ArbitrationLegStatistic.CurrentPosition == null)
                        {
                            writer.WriteLine("None");
                        }
                        else
                        {
                            writer.WriteLine(leg.ArbitrationLegStatistic.CurrentPosition.Number.ToString());
                        }

                        writer.WriteLine(leg.ArbitrationLegStatistic.Status.ToString());
                        writer.WriteLine(leg.ArbitrationLegStatistic.OpenVolume.ToString());
                        writer.WriteLine(leg.ArbitrationLegStatistic.Side.ToString());
                        writer.WriteLine(leg.TotalVolume.ToString());

                        writer.WriteLine(leg.EnterArbitrationSteps.Count.ToString());
                        WriteArbitrationSteps(leg.EnterArbitrationSteps, writer);

                        writer.WriteLine(leg.ExitArbitrationSteps.Count.ToString());
                        WriteArbitrationSteps(leg.ExitArbitrationSteps, writer);

                        writer.WriteLine("/");
                        writer.WriteLine("/");
                        writer.WriteLine("/");
                    }

                    for (int i = 0; i < _secondaryLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _secondaryLegs[i];

                        writer.WriteLine(leg.AssetPortfolio.ToString());
                        writer.WriteLine(leg.EnterLifetimeOrder.ToString());
                        writer.WriteLine(leg.EnterOneOrderVolume.ToString());
                        writer.WriteLine(leg.EnterOrderFrequency.ToString());
                        writer.WriteLine(leg.EnterOrderPosition.ToString());
                        writer.WriteLine(leg.EnterOrderType.ToString());
                        writer.WriteLine(leg.EnterSlippage.ToString());
                        writer.WriteLine(leg.ExitLifetimeOrder.ToString());
                        writer.WriteLine(leg.ExitOneOrderVolume.ToString());
                        writer.WriteLine(leg.ExitOrderFrequency.ToString());
                        writer.WriteLine(leg.ExitOrderPosition.ToString());
                        writer.WriteLine(leg.ExitOrderType.ToString());
                        writer.WriteLine(leg.ExitSlippage.ToString());
                        writer.WriteLine(leg.VolumeType.ToString());
                        writer.WriteLine(leg.BotTab.TabName.ToString());

                        if (leg.ArbitrationLegStatistic.CurrentPosition == null)
                        {
                            writer.WriteLine("None");
                        }
                        else
                        {
                            writer.WriteLine(leg.ArbitrationLegStatistic.CurrentPosition.Number.ToString());
                        }

                        writer.WriteLine(leg.ArbitrationLegStatistic.Status.ToString());
                        writer.WriteLine(leg.ArbitrationLegStatistic.OpenVolume.ToString());
                        writer.WriteLine(leg.ArbitrationLegStatistic.Side.ToString());
                        writer.WriteLine(leg.TotalVolume.ToString());

                        writer.WriteLine(leg.EnterArbitrationSteps.Count.ToString());
                        WriteArbitrationSteps(leg.EnterArbitrationSteps, writer);

                        writer.WriteLine(leg.ExitArbitrationSteps.Count.ToString());
                        WriteArbitrationSteps(leg.ExitArbitrationSteps, writer);

                        writer.WriteLine("/");
                        writer.WriteLine("/");
                        writer.WriteLine("/");
                    }
                }
            }
        }

        private void WriteArbitrationSteps(List<ArbitrationStep> steps, StreamWriter writer)
        {
            for (int i2 = 0; i2 < steps.Count; i2++)
            {
                ArbitrationStep step = steps[i2];

                writer.WriteLine(step.UniqStepName.ToString());
                writer.WriteLine(step.NumberStep.ToString());
                writer.WriteLine(step.Status.ToString());
                writer.WriteLine(step.LastUpdateTime.ToString());
                writer.WriteLine(step.OpenVolume.ToString());
                writer.WriteLine(step.StartOpenVolume.ToString());
                writer.WriteLine(step.TimeActivateStep.ToString());
                writer.WriteLine(step.VolumeStep.ToString());
            }
        }

        public void AddMainLeg(ArbitrationLeg leg)
        {
            if (_mainLegs == null)
                _mainLegs = new List<ArbitrationLeg>();

            _mainLegs.Add(leg);
        }

        public void AddSecondaryLeg(ArbitrationLeg leg)
        {
            if (_secondaryLegs == null)
                _secondaryLegs = new List<ArbitrationLeg>();

            _secondaryLegs.Add(leg);
        }

        public void Delete()
        {
            try
            {
                if (_isDisposed == false)
                    _isDisposed = true;

                for (int i = 0; i < _mainLegs.Count; i++)
                {
                    ArbitrationLeg leg = _mainLegs[i];

                    if (leg.BotTab == null)
                        continue;

                    leg.BotTab.Delete();
                }

                for (int i = 0; i < _secondaryLegs.Count; i++)
                {
                    ArbitrationLeg leg = _secondaryLegs[i];

                    if (leg.BotTab == null)
                        continue;

                    leg.BotTab.Delete();
                }

                if (File.Exists(@"Engine\" + UniqueName + @"ToLoad.txt"))
                {
                    File.Delete(@"Engine\" + UniqueName + @"ToLoad.txt");
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _mainLegs.Count; i++)
            {
                ArbitrationLeg leg = _mainLegs[i];

                if (leg.BotTab == null)
                    continue;

                leg.BotTab.Clear();
            }

            for (int i = 0; i < _secondaryLegs.Count; i++)
            {
                ArbitrationLeg leg = _secondaryLegs[i];

                if (leg.BotTab == null)
                    continue;

                leg.BotTab.Clear();
            }

            if (_isDisposed == false)
                _isDisposed = true;
        }

        #endregion

        #region Main thread

        public void Start(ArbitrationMode mode)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                if (CheckTradingReady() == false)
                {
                    string message = "Арбитражный модуль не готов к торговле";
                    InfoLogEvent?.Invoke(message);
                    return;
                }

                if (mode == ArbitrationMode.CloseScript ||
            mode == ArbitrationMode.CloseAllScripts)
                {
                    CreateLegSteps(isOpen: false, mode);
                }
                else
                {
                    CreateLegSteps(isOpen: true, mode);
                }

                if (_isDisposed == true)
                {
                    if (_mainThread == null)
                    {
                        _mainThread = new Thread(MainThread);
                        _mainThread.IsBackground = true;
                        _mainThread.Start();

                        _isDisposed = false;
                    }
                }

                _currentMode = mode;
                _currentStatus = ArbitrationStatus.On;

                Save();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
            }
        }

        public void Stop()
        {
            _isDisposed = true;
            _currentStatus = ArbitrationStatus.Pause;
        }

        private void MainThread()
        {
            while (!_isDisposed)
            {
                try
                {
                    _currentTimeServer = DateTime.UtcNow.AddHours(ShiftTime);

                    if (!CheckTradingReady())
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (!CheckNonTradePeriod())
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_currentStatus == ArbitrationStatus.On)
                    {
                        OnLogic();
                    }
                    else if (_currentStatus == ArbitrationStatus.Pause)
                    {
                        PauseLogic();
                    }
                    else if (_currentStatus == ArbitrationStatus.Off)
                    {
                        OffLogic();
                    }

                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                    InfoLogEvent?.Invoke(ex.ToString());
                    Thread.Sleep(5000);
                }
            }
        }

        private void OnLogic()
        {
            try
            {
                if (_currentMode == ArbitrationMode.CloseScript || _currentMode == ArbitrationMode.CloseAllScripts)
                    ClosePositionLogic();
                else
                    OpenPositionLogic();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private void ClosePositionLogic()
        {
            try
            {
                bool mainStepsIsDone = true;
                bool secondaryStepsIsDone = true;

                for (int i = 0; i < _mainLegs.Count; i++)
                {
                    ArbitrationLeg leg = _mainLegs[i];

                    if (leg.ArbitrationLegStatistic.CurrentPosition == null) continue;
                    else if (leg.ExitArbitrationSteps.Count == 0)
                    {
                        CreateLegSteps(isOpen: false, _currentMode);
                    }

                    UpdateStep(ref leg, isOpen: false);

                    if (leg.CurrentStep.Status != OrderStateType.Done)
                    {
                        TryClosePosition(leg);

                        mainStepsIsDone = false;
                    }
                }

                if (mainStepsIsDone)
                {
                    for (int i = 0; i < _secondaryLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _secondaryLegs[i];

                        if (leg.ArbitrationLegStatistic.CurrentPosition == null) continue;
                        else if (leg.ExitArbitrationSteps.Count == 0)
                        {
                            CreateLegSteps(isOpen: false, _currentMode);
                        }

                        UpdateStep(ref leg, isOpen: false);

                        if (leg.CurrentStep.Status != OrderStateType.Done)
                        {
                            TryClosePosition(leg);

                            secondaryStepsIsDone = false;
                        }
                    }
                }

                if (mainStepsIsDone && secondaryStepsIsDone)
                {
                    for (int i = 0; i < _mainLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _mainLegs[i];

                        for (int i2 = 0; i2 < leg.ExitArbitrationSteps.Count; i2++)
                        {
                            ArbitrationStep step = leg.ExitArbitrationSteps[i2];

                            if (step.Status == OrderStateType.Done)
                                continue;

                            step.TimeActivateStep = CurrentTimeServer;

                            leg.CurrentStep = step;
                            break;
                        }
                    }

                    for (int i = 0; i < _secondaryLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _secondaryLegs[i];

                        for (int i2 = 0; i2 < leg.ExitArbitrationSteps.Count; i2++)
                        {
                            ArbitrationStep step = leg.ExitArbitrationSteps[i2];

                            if (step.Status == OrderStateType.Done)
                                continue;

                            step.TimeActivateStep = CurrentTimeServer;

                            leg.CurrentStep = step;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private void TryClosePosition(ArbitrationLeg leg)
        {
            try
            {
                if (leg.ArbitrationLegStatistic.CurrentPosition.CloseActive)
                {
                    Order activeCloseOrder = leg.ArbitrationLegStatistic.CurrentPosition.CloseOrders[^1];
                    if (activeCloseOrder != null)
                    {
                        if (leg.ExitOrderType == OrderPriceType.Market)
                            return;

                        if (activeCloseOrder.IsSendToCancel
                            && activeCloseOrder.LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                            return;

                        if (!string.IsNullOrEmpty(activeCloseOrder.NumberMarket)
                            && leg.ExitLifetimeOrder > 0
                            && (leg.BotTab.TimeServerCurrent - activeCloseOrder.TimeCreate).TotalSeconds >= leg.ExitLifetimeOrder)
                            leg.BotTab.CloseOrder(activeCloseOrder);
                    }
                    return;
                }

                decimal closeVolume = leg.CurrentStep.VolumeStep - leg.CurrentStep.OpenVolume;
                if (closeVolume <= 0)
                    return;

                if (leg.ExitOrderFrequency > 0
                && leg.LastExitOrderTime != DateTime.MinValue
                && (_currentTimeServer - leg.LastExitOrderTime).TotalSeconds < leg.ExitOrderFrequency)
                    return;

                if (leg.ExitOrderType == OrderPriceType.Market)
                {
                    leg.BotTab.CloseAtMarket(leg.ArbitrationLegStatistic.CurrentPosition, closeVolume);
                }
                else
                {
                    decimal price = CalculateExitLimitPrice(leg, leg.ArbitrationLegStatistic.CurrentPosition.Direction);
                    if (price > 0)
                        leg.BotTab.CloseAtLimit(leg.ArbitrationLegStatistic.CurrentPosition, price, closeVolume);
                }

                leg.LastExitOrderTime = _currentTimeServer;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private decimal CalculateExitLimitPrice(ArbitrationLeg leg, Side positionDirection)
        {
            try
            {
                decimal basePrice = CalculateBasePrice(leg.BotTab, leg.ExitOrderPosition);
                if (basePrice <= 0)
                    return 0;

                return positionDirection == Side.Buy
                    ? basePrice - leg.ExitSlippage
                    : basePrice + leg.ExitSlippage;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
                return 0;
            }
        }

        private void UpdateStep(ref ArbitrationLeg leg, bool isOpen)
        {
            try
            {
                List<ArbitrationStep> steps = null;

                if (isOpen)
                    steps = leg.EnterArbitrationSteps;
                else
                    steps = leg.ExitArbitrationSteps;

                for (int i = 0; i < steps.Count; i++)
                {
                    ArbitrationStep step = steps[i];

                    if (i == 0 && leg.CurrentStep == null)
                    {
                        step.TimeActivateStep = CurrentTimeServer;

                        if (leg.ArbitrationLegStatistic.CurrentPosition == null)
                            step.StartOpenVolume = 0;
                        else
                            step.StartOpenVolume = leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume;

                        step.Status = OrderStateType.Active;
                        leg.CurrentStep = step;
                    }
                    else
                    {
                        if (leg.CurrentStep.UniqStepName == step.UniqStepName)
                        {
                            if (isOpen &&
                                leg.ArbitrationLegStatistic.CurrentPosition == null)
                            {
                                break;
                            }

                            if (isOpen &&
                                leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume - leg.CurrentStep.StartOpenVolume < leg.CurrentStep.VolumeStep)
                            {
                                leg.CurrentStep.OpenVolume = leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume - leg.CurrentStep.StartOpenVolume;
                                leg.CurrentStep.Status = OrderStateType.Active;
                            }
                            else if (isOpen &&
                                 leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume - leg.CurrentStep.StartOpenVolume >= leg.CurrentStep.VolumeStep)
                            {
                                leg.CurrentStep.OpenVolume = leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume - leg.CurrentStep.StartOpenVolume;
                                leg.CurrentStep.Status = OrderStateType.Done;
                            }
                            else if (!isOpen &&
                                leg.CurrentStep.StartOpenVolume - leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume < leg.CurrentStep.VolumeStep)
                            {
                                leg.CurrentStep.OpenVolume = leg.CurrentStep.StartOpenVolume - leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume;
                                leg.CurrentStep.Status = OrderStateType.Active;
                            }
                            else if (!isOpen &&
                                leg.CurrentStep.StartOpenVolume - leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume >= leg.CurrentStep.VolumeStep)
                            {
                                leg.CurrentStep.OpenVolume = leg.CurrentStep.StartOpenVolume - leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume;
                                leg.CurrentStep.Status = OrderStateType.Done;
                            }

                            leg.ArbitrationLegStatistic.OpenVolume = leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume;

                            bool isUpdate = false;

                            if (step.Status != leg.CurrentStep.Status)
                            {
                                step.Status = leg.CurrentStep.Status;
                                isUpdate = true;
                            }
                            if (step.OpenVolume != leg.CurrentStep.OpenVolume)
                            {
                                step.OpenVolume = leg.CurrentStep.OpenVolume;
                                isUpdate = true;
                            }
                            if (step.LastUpdateTime != leg.CurrentStep.LastUpdateTime)
                            {
                                step.LastUpdateTime = leg.CurrentStep.LastUpdateTime;
                                isUpdate = true;
                            }
                            if (step.TimeActivateStep != leg.CurrentStep.TimeActivateStep)
                            {
                                step.TimeActivateStep = leg.CurrentStep.TimeActivateStep;
                                isUpdate = true;
                            }
                            if (step.VolumeStep != leg.CurrentStep.VolumeStep)
                            {
                                step.VolumeStep = leg.CurrentStep.VolumeStep;
                                isUpdate = true;
                            }
                            if (step.NumberStep != leg.CurrentStep.NumberStep)
                            {
                                step.NumberStep = leg.CurrentStep.NumberStep;
                                isUpdate = true;
                            }

                            if (isUpdate)
                                Save();

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private void OpenPositionLogic()
        {
            try
            {
                bool mainStepsIsDone = true;
                bool secondaryStepsIsDone = true;

                for (int i = 0; i < _mainLegs.Count; i++)
                {
                    ArbitrationLeg leg = _mainLegs[i];

                    if (leg.EnterArbitrationSteps.Count == 0)
                    {
                        CreateLegSteps(isOpen: true, _currentMode);
                    }

                    UpdateStep(ref leg, isOpen: true);

                    if (leg.CurrentStep.Status != OrderStateType.Done)
                    {
                        TryOpenPosition(ref leg);

                        mainStepsIsDone = false;
                    }
                }

                if (mainStepsIsDone)
                {
                    for (int i = 0; i < _secondaryLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _secondaryLegs[i];

                        if (leg.EnterArbitrationSteps.Count == 0)
                        {
                            CreateLegSteps(isOpen: true, _currentMode);
                        }

                        UpdateStep(ref leg, isOpen: true);

                        if (leg.CurrentStep.Status != OrderStateType.Done)
                        {
                            TryOpenPosition(ref leg);

                            secondaryStepsIsDone = false;
                        }
                    }
                }
                else secondaryStepsIsDone = false;

                if (mainStepsIsDone && secondaryStepsIsDone)
                {
                    for (int i = 0; i < _mainLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _mainLegs[i];

                        for (int i2 = 0; i2 < leg.EnterArbitrationSteps.Count; i2++)
                        {
                            ArbitrationStep step = leg.EnterArbitrationSteps[i2];

                            if (step.Status == OrderStateType.Done)
                                continue;

                            leg.CurrentStep = step;
                            break;
                        }
                    }

                    for (int i = 0; i < _secondaryLegs.Count; i++)
                    {
                        ArbitrationLeg leg = _secondaryLegs[i];

                        for (int i2 = 0; i2 < leg.EnterArbitrationSteps.Count; i2++)
                        {
                            ArbitrationStep step = leg.EnterArbitrationSteps[i2];

                            if (step.Status == OrderStateType.Done)
                                continue;

                            leg.CurrentStep = step;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private void TryOpenPosition(ref ArbitrationLeg leg)
        {
            try
            {
                if (leg.CurrentStep == null)
                    return;

                Side side = leg.ArbitrationLegStatistic.Side;

                decimal orderVolume = 0;

                if (leg.ArbitrationLegStatistic.CurrentPosition != null)
                    orderVolume = leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume - leg.CurrentStep.StartOpenVolume + leg.CurrentStep.VolumeStep;
                else
                    orderVolume = leg.CurrentStep.VolumeStep;

                if (leg.EnterOrderFrequency > 0
                && leg.LastEnterOrderTime != DateTime.MinValue
                && (DateTime.Now - leg.LastEnterOrderTime).TotalSeconds < leg.EnterOrderFrequency)
                    return;

                PlaceOpenOrder(ref leg, leg.ArbitrationLegStatistic.Side, orderVolume);

            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private void PlaceOpenOrder(ref ArbitrationLeg leg, Side side, decimal orderVolume)
        {
            if (leg.ArbitrationLegStatistic.CurrentPosition == null)
            {
                if (leg.EnterOrderType == OrderPriceType.Limit)
                {
                    decimal price = CalculateEnterLimitPrice(leg, leg.ArbitrationLegStatistic.Side);
                    if (price <= 0)
                        return;

                    leg.ArbitrationLegStatistic.CurrentPosition = side == Side.Buy
                    ? leg.BotTab.BuyAtLimit(leg.CurrentStep.VolumeStep, price)
                    : leg.BotTab.SellAtLimit(leg.CurrentStep.VolumeStep, price);

                    leg.CurrentStep.LastUpdateTime = _currentTimeServer;
                    leg.CurrentStep.TimeActivateStep = _currentTimeServer;
                    leg.CurrentStep.Status = leg.ArbitrationLegStatistic.CurrentPosition.OpenOrders[^1].State;
                }
                else if (leg.EnterOrderType == OrderPriceType.Market)
                {
                    leg.ArbitrationLegStatistic.CurrentPosition = side == Side.Buy
                    ? leg.BotTab.BuyAtMarket(leg.CurrentStep.VolumeStep)
                    : leg.BotTab.SellAtMarket(leg.CurrentStep.VolumeStep);

                    leg.CurrentStep.LastUpdateTime = _currentTimeServer;
                    leg.CurrentStep.TimeActivateStep = _currentTimeServer;
                    leg.CurrentStep.Status = leg.ArbitrationLegStatistic.CurrentPosition.OpenOrders[^1].State;
                }

                leg.LastEnterOrderTime = DateTime.Now;

                return;
            }
            else
            {
                if (leg.EnterOrderType == OrderPriceType.Market)
                {
                    if (side == Side.Buy)
                        leg.BotTab.BuyAtMarketToPosition(leg.ArbitrationLegStatistic.CurrentPosition, orderVolume);
                    else
                        leg.BotTab.SellAtMarketToPosition(leg.ArbitrationLegStatistic.CurrentPosition, orderVolume);
                }
                else
                {
                    decimal price = CalculateEnterLimitPrice(leg, side);
                    if (price <= 0)
                        return;

                    if (side == Side.Buy)
                        leg.BotTab.BuyAtLimitToPosition(leg.ArbitrationLegStatistic.CurrentPosition, price, orderVolume);
                    else
                        leg.BotTab.SellAtLimitToPosition(leg.ArbitrationLegStatistic.CurrentPosition, price, orderVolume);
                }

                leg.LastEnterOrderTime = DateTime.Now;
            }
        }

        private decimal CalculateEnterLimitPrice(ArbitrationLeg leg, Side side)
        {
            decimal basePrice = CalculateBasePrice(leg.BotTab, leg.EnterOrderPosition);
            if (basePrice <= 0)
                return 0;

            return side == Side.Buy
                ? basePrice + leg.EnterSlippage
                : basePrice - leg.EnterSlippage;
        }

        private decimal CalculateBasePrice(BotTabSimple botTab, SynteticBondOrderPosition position)
        {
            decimal bestBid = botTab.PriceBestBid;
            decimal bestAsk = botTab.PriceBestAsk;

            if (bestBid <= 0 || bestAsk <= 0)
                return 0;

            if (position == SynteticBondOrderPosition.Ask) return bestAsk;
            if (position == SynteticBondOrderPosition.Bid) return bestBid;
            return (bestAsk + bestBid) / 2;
        }

        private void PauseLogic()
        {
            try
            {

            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        private void OffLogic()
        {
            try
            {

            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(5000);
            }
        }

        #endregion

        #region Create steps

        private void CreateLegSteps(bool isOpen, ArbitrationMode mode)
        {
            try
            {
                Side mainSide = Side.None;
                Side secxondarySide = Side.None;

                if (mode == ArbitrationMode.OpenBuyFirstSellSecond)
                {
                    mainSide = Side.Buy;
                    secxondarySide = Side.Sell;
                }
                else if (mode == ArbitrationMode.OpenSellFirstBuySecond)
                {
                    mainSide = Side.Sell;
                    secxondarySide = Side.Buy;
                }

                for (int i = 0; i < _mainLegs.Count; i++)
                {
                    ArbitrationLeg leg = _mainLegs[i];

                    leg.CurrentStep = null;

                    CreateSteps(leg, isOpen, mainSide);
                }

                for (int i = 0; i < _secondaryLegs.Count; i++)
                {
                    ArbitrationLeg leg = _secondaryLegs[i];

                    leg.CurrentStep = null;

                    CreateSteps(leg, isOpen, secxondarySide);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
            }
        }

        private void CreateSteps(ArbitrationLeg leg, bool isOpen, Side side = Side.None)
        {
            try
            {
                List<ArbitrationStep> steps = new List<ArbitrationStep>();

                decimal totalVolume = GetTotalVolume(leg, isOpen);

                if (side != Side.None)
                    leg.ArbitrationLegStatistic.Side = side;

                leg.ArbitrationLegStatistic.TotalVolumeLot = totalVolume;
                leg.ArbitrationLegStatistic.Status = OrderStateType.Active;

                decimal stepVolume = 0;

                if (isOpen)
                {
                    if (leg.EnterOneOrderVolume == leg.TotalVolume)
                        stepVolume = totalVolume;
                    else
                        stepVolume = GetEnterVolume(leg);
                }
                else
                {
                    if (leg.ExitOneOrderVolume == leg.TotalVolume)
                        stepVolume = totalVolume;
                    else
                        stepVolume = GetExitVolume(leg);
                }

                decimal remainingVolume = leg.ArbitrationLegStatistic.TotalVolumeLot;
                int stepNumber = 1;

                if (leg.ArbitrationLegStatistic.CurrentPosition != null)
                {
                    remainingVolume = leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume;
                }

                decimal lastVolume = 0;
                decimal reverseRemainingVolume = 0;

                while (remainingVolume > 0)
                {
                    decimal currentStepVolume = 0;
                    if (remainingVolume != 0)
                        currentStepVolume = Math.Min(stepVolume, remainingVolume);
                    else
                        currentStepVolume = stepVolume;

                    ArbitrationStep step = new ArbitrationStep();
                    step.NumberStep = stepNumber;
                    step.UniqStepName = leg.BotTab.NameStrategy + step.NumberStep;
                    step.VolumeStep = currentStepVolume;
                    step.OpenVolume = 0m;
                    step.Status = OrderStateType.None;

                    reverseRemainingVolume += lastVolume;
                    lastVolume = step.VolumeStep;

                    if (isOpen)
                        step.StartOpenVolume = reverseRemainingVolume;
                    else
                        step.StartOpenVolume = remainingVolume;

                    steps.Add(step);

                    remainingVolume -= currentStepVolume;

                    if (isOpen && remainingVolume <= 0)
                        break;

                    stepNumber++;
                }

                if (isOpen)
                    leg.EnterArbitrationSteps = steps;
                else leg.ExitArbitrationSteps = steps;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
            }
        }

        private decimal GetTotalVolume(ArbitrationLeg leg, bool isOpen)
        {
            if (isOpen)
                return CalculateVolume(leg, leg.VolumeType, leg.TotalVolume);
            else
                return leg.ArbitrationLegStatistic.CurrentPosition.OpenVolume;
        }

        private decimal GetEnterVolume(ArbitrationLeg leg)
        {
            return CalculateVolume(leg, leg.VolumeType, leg.EnterOneOrderVolume);
        }

        private decimal GetExitVolume(ArbitrationLeg leg)
        {
            return CalculateVolume(leg, leg.VolumeType, leg.ExitOneOrderVolume);
        }

        private decimal CalculateVolume(ArbitrationLeg param, VolumeType volumeType, decimal incomingVolume)
        {
            if (volumeType == VolumeType.Contracts)
            {
                return incomingVolume;
            }
            else if (volumeType == VolumeType.ContractCurrency)
            {
                decimal contractPrice = param.BotTab.PriceBestAsk;
                if (contractPrice <= 0)
                    return 0;

                decimal volume = incomingVolume / contractPrice;

                if (param.BotTab.StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(param.BotTab.Connector.ServerType);

                    if (serverPermission != null
                        && serverPermission.IsUseLotToCalculateProfit
                        && param.BotTab.Security.Lot != 0
                        && param.BotTab.Security.Lot > 1)
                    {
                        volume = incomingVolume / (contractPrice * param.BotTab.Security.Lot);
                    }

                    volume = Math.Round(volume, param.BotTab.Security.DecimalsVolume);
                }
                else
                {
                    volume = Math.Round(volume, 6);
                }

                return volume;
            }
            else if (volumeType == VolumeType.DepositPercent)
            {
                Portfolio myPortfolio = param.BotTab.Portfolio;
                if (myPortfolio == null)
                    return 0;

                decimal portfolioPrimeAsset = 0;

                if (param.AssetPortfolio == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                        return 0;

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == param.AssetPortfolio)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    ServerMaster.SendNewLogMessage("Can`t found portfolio " + param.AssetPortfolio, LogMessageType.Error);
                    return 0;
                }

                if (param.BotTab.PriceBestAsk <= 0)
                    return 0;

                decimal moneyOnPosition = portfolioPrimeAsset * (incomingVolume / 100);
                decimal qty = moneyOnPosition / param.BotTab.PriceBestAsk / param.BotTab.Security.Lot;

                if (param.BotTab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (param.BotTab.Security.UsePriceStepCostToCalculateVolume
                        && param.BotTab.Security.PriceStep != param.BotTab.Security.PriceStepCost
                        && param.BotTab.PriceBestAsk != 0
                        && param.BotTab.Security.PriceStep != 0
                        && param.BotTab.Security.PriceStepCost != 0)
                    {
                        qty = moneyOnPosition / (param.BotTab.PriceBestAsk / param.BotTab.Security.PriceStep * param.BotTab.Security.PriceStepCost);
                    }

                    qty = Math.Round(qty, param.BotTab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return incomingVolume;
        }

        #endregion

        #region Trade helpers

        public bool CheckTradingReady()
        {
            if (_mainLegs == null)
            {
                return false;
            }
            else if (_mainLegs.Count == 0)
            {
                return false;
            }
            else if (_secondaryLegs == null)
            {
                return false;
            }
            else if (_secondaryLegs.Count == 0)
            {
                return false;
            }
            else if (LegsIsReadyToTrade(_mainLegs) == false)
            {
                return false;
            }
            else if (LegsIsReadyToTrade(_secondaryLegs) == false)
            {
                return false;
            }

            return true;
        }

        private bool LegsIsReadyToTrade(List<ArbitrationLeg> legs)
        {
            for (int i = 0; i < legs.Count; i++)
            {
                ArbitrationLeg leg = legs[i];

                if (leg == null)
                {
                    return false;
                }
                else if (leg.BotTab == null)
                {
                    return false;
                }
                else if (leg.BotTab.IsReadyToTrade == false)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckNonTradePeriod()
        {
            if (_nonTradePeriods == null)
            {
                if (_isNonTradePeriod != false) { _isNonTradePeriod = true; }
                return false;
            }

            if (_currentTimeServer == DateTime.MinValue)
            {
                if (_isNonTradePeriod != false) { _isNonTradePeriod = true; }
                return false;
            }

            bool canTrade = NonTradePeriods.CanTradeThisTime(_currentTimeServer);

            if (canTrade == false)
            {
                if (_isNonTradePeriod == false)
                    _isNonTradePeriod = true;
                else return false;

                string message = "Наступил неторговый период";
                InfoLogEvent?.Invoke(message);

                return false;
            }
            else
            {
                if (_isNonTradePeriod == true)
                    _isNonTradePeriod = false;
                else return true;

                string message = "Неторговый период завершён. Восстановлен режим: " + CurrentMode.ToString()
                    + ", статус: " + CurrentStatus.ToString();
                InfoLogEvent?.Invoke(message);
                return true;
            }
        }

        #endregion

        #region Public properties

        public string UniqueName;
        private StartProgram StartProgram;

        /// <summary>
        /// Main legs | Основные ноги
        /// </summary>
        public List<ArbitrationLeg> MainLegs
        {
            get { return _mainLegs; }
        }

        /// <summary>
        /// Secondary legs | Второстепенные ноги
        /// </summary>
        public List<ArbitrationLeg> SecondaryLegs
        {
            get { return _secondaryLegs; }
        }

        /// <summary>
        /// Current status of arbitration | Текущий статус арбитража
        /// </summary>
        public ArbitrationStatus CurrentStatus
        {
            get { return _currentStatus; }
            set { _currentStatus = value; }
        }

        /// <summary>
        /// Current arbitration mode | Текущий режим арбитража
        /// </summary>
        public ArbitrationMode CurrentMode
        {
            get { return _currentMode; }
            set { _currentMode = value; }
        }

        public NonTradePeriods NonTradePeriods
        {
            get { return _nonTradePeriods; }
            set { _nonTradePeriods = value; }
        }

        /// <summary>
        /// Is there a non-trading period currently in effect | Действует ли сейчас неторговый период
        /// </summary>
        public bool IsNonTradePeriod
        {
            get { return _isNonTradePeriod; }
        }

        public int ShiftTime = 0;

        public DateTime CurrentTimeServer
        {
            get { return _currentTimeServer; }
        }

        public bool StopTradeOnFailOrders
        {
            get { return _stopTradeOnFailOrders; }
            set { _stopTradeOnFailOrders = value; }
        }

        #endregion

        #region Private fields

        private Thread _mainThread;

        private bool _isDisposed = true;

        private ArbitrationStatus _currentStatus;

        private ArbitrationMode _currentMode;

        private NonTradePeriods _nonTradePeriods;

        private bool _isNonTradePeriod;

        private List<ArbitrationLeg> _mainLegs;

        private List<ArbitrationLeg> _secondaryLegs;

        private DateTime _currentTimeServer;

        private bool _stopTradeOnFailOrders;

        private bool _allPositionsFilled;

        private readonly object _fileLock = new object();

        #endregion

        #region Events

        public Action<string> InfoLogEvent;

        public Action AllPositionsFilledEvent;

        public Action AllPositionsClosedEvent;

        #endregion
    }

    public enum ArbitrationStatus
    {
        Off,

        On,

        Pause,
    }

    public enum ArbitrationMode
    {
        OpenBuyFirstSellSecond,

        OpenSellFirstBuySecond,

        CloseScript,

        CloseAllScripts
    }
}
