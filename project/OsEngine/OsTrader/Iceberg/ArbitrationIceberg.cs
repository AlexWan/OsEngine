using OsEngine.Entity;
using OsEngine.Entity.SynteticBondEntity;
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

        public ArbitrationIceberg(string name)
        {
            NameUnique = name + "ArbitrationIceberg";

            _mainTab = new ArbitrationParameters();
            _secondaryTab = new List<ArbitrationParameters>();
            _secondaryTab.Add(new ArbitrationParameters());

            Load();
        }

        #endregion

        #region Save / Load

        public void Save()
        {
            SaveParameters();
            SavePositions();
        }

        private void SaveParameters()
        {
            try
            {
                EnsureDataDirectory();

                using (StreamWriter writer = new StreamWriter(@"Engine\ArbitrationIceberg\" + NameUnique + "Parameters.txt", false))
                {
                    ArbitrationParameters secondary = _secondaryTab.Count > 0
                        ? _secondaryTab[0]
                        : new ArbitrationParameters();

                    writer.WriteLine(secondary.CurrentArbitrationMode.ToString());
                    writer.WriteLine(secondary.CurrentArbitrationStatus.ToString());

                    WriteArbitrationParams(writer, _mainTab);

                    writer.WriteLine(_secondaryTab.Count.ToString());
                    for (int i = 0; i < _secondaryTab.Count; i++)
                        WriteArbitrationParams(writer, _secondaryTab[i]);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void SavePositions()
        {
            try
            {
                EnsureDataDirectory();

                using (StreamWriter writer = new StreamWriter(@"Engine\ArbitrationIceberg\" + NameUnique + "Positions.txt", false))
                {
                    writer.WriteLine(_mainTab.CurrentPosition != null
                        ? _mainTab.CurrentPosition.Number.ToString()
                        : "-1");

                    writer.WriteLine(_secondaryTab.Count.ToString());
                    for (int i = 0; i < _secondaryTab.Count; i++)
                    {
                        writer.WriteLine(_secondaryTab[i].CurrentPosition != null
                            ? _secondaryTab[i].CurrentPosition.Number.ToString()
                            : "-1");
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private void WriteArbitrationParams(StreamWriter writer, ArbitrationParameters p)
        {
            writer.WriteLine(p.TotalVolume.ToString());
            writer.WriteLine(p.EnterOneOrderVolume.ToString());
            writer.WriteLine(p.ExitOneOrderVolume.ToString());
            writer.WriteLine(p.EnterOrderType.ToString());
            writer.WriteLine(p.ExitOrderType.ToString());
            writer.WriteLine(p.EnterOrderPosition.ToString());
            writer.WriteLine(p.ExitOrderPosition.ToString());
            writer.WriteLine(p.EnterSlippage.ToString());
            writer.WriteLine(p.ExitSlippage.ToString());
            writer.WriteLine(p.EnterLifetimeOrder.ToString());
            writer.WriteLine(p.ExitLifetimeOrder.ToString());
            writer.WriteLine(p.EnterOrderFrequency.ToString());
            writer.WriteLine(p.ExitOrderFrequency.ToString());
            writer.WriteLine(p.VolumeType.ToString());
            writer.WriteLine(p.AssetPortfolio ?? string.Empty);
        }

        public void Load()
        {
            LoadParameters();
            LoadPositions();
        }

        private void EnsureDataDirectory()
        {
            if (!Directory.Exists(@"Engine\ArbitrationIceberg"))
                Directory.CreateDirectory(@"Engine\ArbitrationIceberg");
        }

        private void LoadParameters()
        {
            if (!File.Exists(@"Engine\ArbitrationIceberg\" + NameUnique + "Parameters.txt"))
                return;

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\ArbitrationIceberg\" + NameUnique + "Parameters.txt"))
                {
                    string modeLine = reader.ReadLine();
                    string statusLine = reader.ReadLine();

                    if (_secondaryTab.Count > 0)
                    {
                        if (modeLine == "OpenBuyFirstSellSecond")
                            _secondaryTab[0].CurrentArbitrationMode = ArbitrationMode.OpenBuyFirstSellSecond;
                        else if (modeLine == "OpenSellFirstBuySecond")
                            _secondaryTab[0].CurrentArbitrationMode = ArbitrationMode.OpenSellFirstBuySecond;
                        else if (modeLine == "ClosePosition")
                            _secondaryTab[0].CurrentArbitrationMode = ArbitrationMode.CloseScript;

                        if (statusLine == "On")
                            _secondaryTab[0].CurrentArbitrationStatus = ArbitrationStatus.On;
                        else
                            _secondaryTab[0].CurrentArbitrationStatus = ArbitrationStatus.Pause;
                    }

                    ReadArbitrationParams(reader, _mainTab);

                    int secCount = Convert.ToInt32(reader.ReadLine());

                    while (_secondaryTab.Count < secCount)
                        _secondaryTab.Add(new ArbitrationParameters());

                    for (int i = 0; i < secCount && i < _secondaryTab.Count; i++)
                        ReadArbitrationParams(reader, _secondaryTab[i]);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void LoadPositions()
        {
            if (!File.Exists(@"Engine\ArbitrationIceberg\" + NameUnique + "Positions.txt"))
                return;

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\ArbitrationIceberg\" + NameUnique + "Positions.txt"))
                {
                    string mainCurrentLine = reader.ReadLine();
                    if (mainCurrentLine == null)
                        return;

                    _savedMainCurrentPositionNumber = Convert.ToInt32(mainCurrentLine);

                    int secCount = Convert.ToInt32(reader.ReadLine());
                    _savedSecondaryCurrentPositionNumbers = new List<int>();
                    for (int i = 0; i < secCount; i++)
                        _savedSecondaryCurrentPositionNumbers.Add(Convert.ToInt32(reader.ReadLine()));
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ReadArbitrationParams(StreamReader reader, ArbitrationParameters parameters)
        {
            parameters.TotalVolume = reader.ReadLine().ToDecimal();
            parameters.EnterOneOrderVolume = reader.ReadLine().ToDecimal();
            parameters.ExitOneOrderVolume = reader.ReadLine().ToDecimal();

            string enterType = reader.ReadLine();
            if (enterType == "Limit") parameters.EnterOrderType = OrderPriceType.Limit;
            else if (enterType == "Market") parameters.EnterOrderType = OrderPriceType.Market;

            string exitType = reader.ReadLine();
            if (exitType == "Limit") parameters.ExitOrderType = OrderPriceType.Limit;
            else if (exitType == "Market") parameters.ExitOrderType = OrderPriceType.Market;

            string enterPos = reader.ReadLine();
            if (enterPos == "Ask") parameters.EnterOrderPosition = SynteticBondOrderPosition.Ask;
            else if (enterPos == "Bid") parameters.EnterOrderPosition = SynteticBondOrderPosition.Bid;
            else if (enterPos == "Middle") parameters.EnterOrderPosition = SynteticBondOrderPosition.Middle;

            string exitPos = reader.ReadLine();
            if (exitPos == "Ask") parameters.ExitOrderPosition = SynteticBondOrderPosition.Ask;
            else if (exitPos == "Bid") parameters.ExitOrderPosition = SynteticBondOrderPosition.Bid;
            else if (exitPos == "Middle") parameters.ExitOrderPosition = SynteticBondOrderPosition.Middle;

            parameters.EnterSlippage = reader.ReadLine().ToDecimal();
            parameters.ExitSlippage = reader.ReadLine().ToDecimal();
            parameters.EnterLifetimeOrder = Convert.ToInt32(reader.ReadLine());
            parameters.ExitLifetimeOrder = Convert.ToInt32(reader.ReadLine());

            parameters.EnterOrderFrequency = Convert.ToInt32(reader.ReadLine());
            parameters.ExitOrderFrequency = Convert.ToInt32(reader.ReadLine());

            string volumeLine = reader.ReadLine();
            if (volumeLine == VolumeType.ContractCurrency.ToString())
                parameters.VolumeType = VolumeType.ContractCurrency;
            else if (volumeLine == VolumeType.DepositPercent.ToString())
                parameters.VolumeType = VolumeType.DepositPercent;
            else
                parameters.VolumeType = VolumeType.Contracts;

            parameters.AssetPortfolio = reader.ReadLine();
        }

        public void Stop()
        {
            _isDisposed = true;
            CurrentStatus = ArbitrationStatus.Pause;
        }

        public void Delete()
        {
            Stop();
            TryDeleteFile(@"Engine\ArbitrationIceberg\" + NameUnique + "Parameters.txt");
            TryDeleteFile(@"Engine\ArbitrationIceberg\" + NameUnique + "Positions.txt");
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Private fields

        private ArbitrationParameters _mainTab;

        private List<ArbitrationParameters> _secondaryTab;

        private int _savedMainCurrentPositionNumber = -1;

        private List<int> _savedSecondaryCurrentPositionNumbers = new List<int>();

        private bool _allPositionsFilled;

        private bool _allPositionsClosed;

        private bool _threadStarted;

        private bool _isDisposed;

        private ArbitrationStatus _currentStatus = ArbitrationStatus.Pause;

        private int _lastFailOrderNumberInOpen = -1;

        private int _lastLoggedEnterStepIndex = -1;

        private int _lastLoggedExitStepIndex = -1;

        #endregion

        #region Public properties

        public string NameUnique;

        public ArbitrationStatus CurrentStatus
        {
            get
            {
                return _currentStatus;
            }
            set
            {
                _currentStatus = value;
                StatusChangedEvent?.Invoke(value);
            }
        }

        public ArbitrationMode CurrentMode
        {
            get
            {
                if (_secondaryTab.Count > 0)
                    return _secondaryTab[0].CurrentArbitrationMode;
                return ArbitrationMode.OpenBuyFirstSellSecond;
            }
            set
            {
                if (_secondaryTab.Count > 0)
                    _secondaryTab[0].CurrentArbitrationMode = value;
            }
        }

        public ArbitrationParameters MainParameters => _mainTab;

        public IReadOnlyList<ArbitrationParameters> SecondaryParameters => _secondaryTab;

        public NonTradePeriods NonTradePeriods;

        public bool IsInNonTradePeriod
        {
            get { return _isInNonTradePeriod; }
        }

        #endregion

        #region Control methods

        public void Start()
        {
            if (!_threadStarted)
            {
                CalculateIcebergSteps();

                _threadStarted = true;
                Thread thread = new Thread(MainArbitrationIcebergThread);
                thread.IsBackground = true;
                thread.Start();
            }

            _lastLoggedEnterStepIndex = -1;
            _lastLoggedExitStepIndex = -1;

            CurrentStatus = ArbitrationStatus.On;
        }

        public void SetBotTabs(BotTabSimple mainBotTab, BotTabSimple secondaryBotTab)
        {
            _mainTab.BotTab = mainBotTab;

            if (_secondaryTab.Count > 0)
                _secondaryTab[0].BotTab = secondaryBotTab;

            Reconnect();
        }

        public void Pause()
        {
            if (CurrentStatus == ArbitrationStatus.Pause)
            {
                return;
            }

            CurrentStatus = ArbitrationStatus.Pause;
        }

        private bool _isInNonTradePeriod;

        private ArbitrationMode _savedModeBeforeNonTrade;

        private ArbitrationStatus _savedStatusBeforeNonTrade;

        private void MainArbitrationIcebergThread()
        {
            while (!_isDisposed)
            {
                try
                {
                    Thread.Sleep(500);

                    CheckNonTradePeriod();

                    if (CurrentStatus == ArbitrationStatus.On)
                    {
                        if (!CheckTradingReady())
                            continue;

                        if (CurrentMode == ArbitrationMode.OpenBuyFirstSellSecond)
                            OpenPositionLogic(Side.Buy, Side.Sell);
                        else if (CurrentMode == ArbitrationMode.OpenSellFirstBuySecond)
                            OpenPositionLogic(Side.Sell, Side.Buy);
                        else if (CurrentMode == ArbitrationMode.CloseScript)
                            ClosePositionLogic();
                    }
                    else if (CurrentStatus == ArbitrationStatus.Pause)
                    {
                        if (!CheckConnectionReady())
                            continue;

                        PauseLogic();
                    }
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                    InfoLogEvent?.Invoke(ex.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        private void CheckNonTradePeriod()
        {
            if (NonTradePeriods == null)
            {
                return;
            }

            if (_mainTab == null || _mainTab.BotTab == null)
            {
                return;
            }

            DateTime serverTime = _mainTab.BotTab.TimeServerCurrent;

            if (serverTime == DateTime.MinValue)
            {
                return;
            }

            bool canTrade = NonTradePeriods.CanTradeThisTime(serverTime);

            if (canTrade == false && _isInNonTradePeriod == false)
            {
                _isInNonTradePeriod = true;
                _savedModeBeforeNonTrade = CurrentMode;
                _savedStatusBeforeNonTrade = CurrentStatus;

                string message = "Наступил неторговый период";
                InfoLogEvent?.Invoke(message);
            }
            else if (canTrade == true && _isInNonTradePeriod == true)
            {
                _isInNonTradePeriod = false;

                CurrentMode = _savedModeBeforeNonTrade;
                CurrentStatus = _savedStatusBeforeNonTrade;

                string message = "Неторговый период завершён. Восстановлен режим: " + CurrentMode.ToString()
                    + ", статус: " + CurrentStatus.ToString();
                InfoLogEvent?.Invoke(message);
            }
        }

        #endregion

        #region Open position logic

        private void ClearDonePositions()
        {
            bool anyReset = false;

            if (ShouldClearPosition(_mainTab))
            {
                _mainTab.CurrentPosition = null;
                anyReset = true;
            }

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                if (ShouldClearPosition(_secondaryTab[i]))
                {
                    _secondaryTab[i].CurrentPosition = null;
                    anyReset = true;
                }
            }

            if (anyReset)
            {
                _allPositionsFilled = false;
                _allPositionsClosed = false;
                SavePositions();
            }
        }

        private void OpenPositionLogic(Side mainSide, Side secondarySide)
        {
            try
            {
                ClearDonePositions();

                int currentStepIndex = GetCurrentOpenStepIndex();

                LogEnterStepTransitions(currentStepIndex);

                if (currentStepIndex >= 0)
                {
                    if (!IsEnterStepComplete(_mainTab, currentStepIndex))
                    {
                        decimal targetVolume = GetCumulativeEnterVolume(_mainTab, currentStepIndex);
                        TryOpenPosition(_mainTab, mainSide, targetVolume);
                    }
                    else
                    {
                        for (int i = 0; i < _secondaryTab.Count; i++)
                        {
                            if (!IsEnterStepComplete(_secondaryTab[i], currentStepIndex))
                            {
                                decimal targetVolume = GetCumulativeEnterVolume(_secondaryTab[i], currentStepIndex);
                                TryOpenPosition(_secondaryTab[i], secondarySide, targetVolume);
                            }
                        }
                    }
                }

                UpdateAllEnterStepStatuses();
                CheckAndFireAllPositionsFilled();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(1000);
            }
        }

        private decimal GetEnterVolume(ArbitrationParameters param)
        {
            return CalculateVolume(param, param.VolumeType, param.EnterOneOrderVolume);
        }

        private decimal GetExitVolume(ArbitrationParameters param)
        {
            return CalculateVolume(param, param.VolumeType, param.ExitOneOrderVolume);
        }

        private decimal GetTotalVolume(ArbitrationParameters param)
        {
            return CalculateVolume(param, param.VolumeType, param.TotalVolume);
        }

        private decimal CalculateVolume(ArbitrationParameters param, VolumeType volumeType, decimal oneOrderVolume)
        {
            if (volumeType == VolumeType.Contracts)
            {
                return oneOrderVolume;
            }
            else if (volumeType == VolumeType.ContractCurrency)
            {
                decimal contractPrice = param.BotTab.PriceBestAsk;
                if (contractPrice <= 0)
                    return 0;

                decimal volume = oneOrderVolume / contractPrice;

                if (param.BotTab.StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(param.BotTab.Connector.ServerType);

                    if (serverPermission != null
                        && serverPermission.IsUseLotToCalculateProfit
                        && param.BotTab.Security.Lot != 0
                        && param.BotTab.Security.Lot > 1)
                    {
                        volume = oneOrderVolume / (contractPrice * param.BotTab.Security.Lot);
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

                decimal moneyOnPosition = portfolioPrimeAsset * (oneOrderVolume / 100);
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

            return oneOrderVolume;
        }

        #endregion

        #region Pause

        private void PauseLogic()
        {
            try
            {
                CancelAllLegsOrders();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(1000);
            }
        }

        #endregion

        #region Off


        #endregion

        #region ClosePosition

        private void ClosePositionLogic()
        {
            try
            {
                int currentStepIndex = GetCurrentCloseStepIndex();

                LogExitStepTransitions(currentStepIndex);

                if (currentStepIndex >= 0)
                {
                    if (!IsExitStepComplete(_mainTab, currentStepIndex))
                    {
                        TryClosePosition(_mainTab);
                    }
                    else
                    {
                        for (int i = 0; i < _secondaryTab.Count; i++)
                        {
                            if (!IsExitStepComplete(_secondaryTab[i], currentStepIndex))
                            {
                                TryClosePosition(_secondaryTab[i]);
                            }
                        }
                    }
                }

                UpdateAllExitStepStatuses();
                CheckAndFireAllPositionsClosed();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                InfoLogEvent?.Invoke(ex.ToString());
                Thread.Sleep(1000);
            }
        }

        private void TryClosePosition(ArbitrationParameters param)
        {
            if (param.CurrentPosition != null && !IsPositionDone(param.CurrentPosition))
                TryClosePositionCore(param, param.CurrentPosition);
        }

        #endregion

        #region Events

        public Action<string> InfoLogEvent;

        public Action AllPositionsFilledEvent;

        public Action AllPositionsClosedEvent;

        public Action<ArbitrationStatus> StatusChangedEvent;

        #endregion

        #region Opening helpers

        private bool IsAllPositionsFilled()
        {
            if (!IsTargetVolumeFilled(_mainTab))
                return false;

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                if (!IsTargetVolumeFilled(_secondaryTab[i]))
                    return false;
            }

            return true;
        }

        private void CheckAndFireAllPositionsFilled()
        {
            if (_allPositionsFilled)
                return;

            if (!IsAllPositionsFilled())
                return;

            _allPositionsFilled = true;
            AllPositionsFilledEvent?.Invoke();
        }

        private bool IsTargetVolumeFilled(ArbitrationParameters param)
        {
            if (param.CurrentPosition == null)
                return false;

            return param.CurrentPosition.OpenVolume >= GetTotalVolume(param);
        }

        private void TryOpenPosition(ArbitrationParameters param, Side side, decimal targetVolume)
        {
            decimal filled = param.CurrentPosition != null ? param.CurrentPosition.OpenVolume : 0;
            decimal remaining = targetVolume - filled;
            if (remaining <= 0)
                return;

            if (param.CurrentPosition != null && param.CurrentPosition.OpenOrders.Count > 0)
            {
                Order lastOrder = param.CurrentPosition.OpenOrders[^1];

                if (lastOrder.State == OrderStateType.Fail)
                {
                    if (lastOrder.NumberUser == _lastFailOrderNumberInOpen)
                    {
                        // ignore
                    }
                    else
                    {
                        _lastFailOrderNumberInOpen = lastOrder.NumberUser;
                        CurrentStatus = ArbitrationStatus.Pause;
                        return;
                    }
                }
                else if (lastOrder.State == OrderStateType.Active
                || lastOrder.State == OrderStateType.Pending
                || lastOrder.State == OrderStateType.None
                || lastOrder.State == OrderStateType.Partial)
                {
                    if (param.EnterOrderType == OrderPriceType.Market)
                        return;

                    if (lastOrder.IsSendToCancel
                        && lastOrder.LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                        return;

                    if (!string.IsNullOrEmpty(lastOrder.NumberMarket)
                        && param.EnterLifetimeOrder > 0
                        && (DateTime.Now - lastOrder.TimeCreate).TotalSeconds >= param.EnterLifetimeOrder)
                        param.BotTab.CloseOrder(lastOrder);

                    return;
                }
            }

            decimal orderVolume = Math.Min(GetEnterVolume(param), remaining);
            if (orderVolume <= 0)
                return;

            if (param.EnterOrderFrequency > 0
                && param.LastEnterOrderTime != DateTime.MinValue
                && (DateTime.Now - param.LastEnterOrderTime).TotalSeconds < param.EnterOrderFrequency)
                return;

            PlaceOpenOrder(param, side, orderVolume);
            param.LastEnterOrderTime = DateTime.Now;
        }

        private void PlaceOpenOrder(ArbitrationParameters param, Side side, decimal orderVolume)
        {
            if (param.CurrentPosition == null)
            {
                if (param.EnterOrderType == OrderPriceType.Market)
                {
                    param.CurrentPosition = side == Side.Buy
                        ? param.BotTab.BuyAtMarket(orderVolume)
                        : param.BotTab.SellAtMarket(orderVolume);
                }
                else
                {
                    decimal price = CalculateEnterLimitPrice(param, side);
                    if (price <= 0)
                        return;

                    param.CurrentPosition = side == Side.Buy
                        ? param.BotTab.BuyAtLimit(orderVolume, price)
                        : param.BotTab.SellAtLimit(orderVolume, price);
                }

                SavePositions();
            }
            else
            {
                if (!IsLastOpenOrderTerminal(param.CurrentPosition))
                    return;

                if (param.EnterOrderType == OrderPriceType.Market)
                {
                    if (side == Side.Buy)
                        param.BotTab.BuyAtMarketToPosition(param.CurrentPosition, orderVolume);
                    else
                        param.BotTab.SellAtMarketToPosition(param.CurrentPosition, orderVolume);
                }
                else
                {
                    decimal price = CalculateEnterLimitPrice(param, side);
                    if (price <= 0)
                        return;

                    if (side == Side.Buy)
                        param.BotTab.BuyAtLimitToPosition(param.CurrentPosition, price, orderVolume);
                    else
                        param.BotTab.SellAtLimitToPosition(param.CurrentPosition, price, orderVolume);
                }
            }
        }

        private Order GetActiveOpenOrder(ArbitrationParameters param)
        {
            if (param.CurrentPosition == null)
                return null;

            return GetLastOrder(param.CurrentPosition.OpenOrders);
        }

        private decimal CalculateEnterLimitPrice(ArbitrationParameters param, Side side)
        {
            decimal basePrice = CalculateBasePrice(param.BotTab, param.EnterOrderPosition);
            if (basePrice <= 0)
                return 0;

            return side == Side.Buy
                ? basePrice + param.EnterSlippage
                : basePrice - param.EnterSlippage;
        }

        #endregion

        #region Closing check helpers

        private bool IsAllPositionsClosed()
        {
            if (_mainTab.CurrentPosition != null && !IsPositionDone(_mainTab.CurrentPosition))
                return false;

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                if (_secondaryTab[i].CurrentPosition != null && !IsPositionDone(_secondaryTab[i].CurrentPosition))
                    return false;
            }

            return true;
        }

        private void CheckAndFireAllPositionsClosed()
        {
            if (_allPositionsClosed)
                return;

            if (!IsAllPositionsClosed())
                return;

            _allPositionsClosed = true;
            AllPositionsClosedEvent?.Invoke();
        }

        #endregion

        #region Closing helpers

        private void TryClosePositionCore(ArbitrationParameters param, Position pos)
        {
            if (pos == null)
                return;

            bool isClosable = pos.State == PositionStateType.Open
                || pos.State == PositionStateType.Opening
                || pos.State == PositionStateType.Closing;

            if (!isClosable)
                return;

            if (pos.CloseActive)
            {
                Order activeCloseOrder = GetActiveCloseOrder(pos);
                if (activeCloseOrder != null)
                {
                    if (param.ExitOrderType == OrderPriceType.Market)
                        return;

                    if (activeCloseOrder.IsSendToCancel
                        && activeCloseOrder.LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                        return;

                    if (!string.IsNullOrEmpty(activeCloseOrder.NumberMarket)
                        && param.ExitLifetimeOrder > 0
                        && (DateTime.Now - activeCloseOrder.TimeCreate).TotalSeconds >= param.ExitLifetimeOrder)
                        param.BotTab.CloseOrder(activeCloseOrder);
                }
                return;
            }

            if (!IsLastCloseOrderTerminal(pos))
                return;

            decimal closeVolume = Math.Min(GetExitVolume(param), pos.OpenVolume);
            if (closeVolume <= 0)
                return;

            if (param.ExitOrderFrequency > 0
                && param.LastExitOrderTime != DateTime.MinValue
                && (DateTime.Now - param.LastExitOrderTime).TotalSeconds < param.ExitOrderFrequency)
                return;

            decimal totalVolume = GetTotalVolume(param);
            decimal exitStepVolume = GetExitVolume(param);
            decimal closedVolume = totalVolume - pos.OpenVolume;
            int currentStep = exitStepVolume > 0 ? (int)(closedVolume / exitStepVolume) + 1 : 1;
            int totalSteps = exitStepVolume > 0 && totalVolume > 0 ? (int)Math.Ceiling(totalVolume / exitStepVolume) : 1;

            if (param.ExitOrderType == OrderPriceType.Market)
            {
                param.BotTab.CloseAtMarket(pos, closeVolume);
            }
            else
            {
                decimal price = CalculateExitLimitPrice(param, pos.Direction);
                if (price > 0)
                    param.BotTab.CloseAtLimit(pos, price, closeVolume);
            }

            param.LastExitOrderTime = DateTime.Now;
        }

        private Order GetActiveCloseOrder(Position pos)
        {
            return GetLastOrder(pos.CloseOrders);
        }

        private decimal CalculateExitLimitPrice(ArbitrationParameters param, Side positionDirection)
        {
            decimal basePrice = CalculateBasePrice(param.BotTab, param.ExitOrderPosition);
            if (basePrice <= 0)
                return 0;

            return positionDirection == Side.Buy
                ? basePrice - param.ExitSlippage
                : basePrice + param.ExitSlippage;
        }

        #endregion

        #region Order / position helpers

        private Order GetLastOrder(List<Order> orders)
        {
            if (orders == null)
                return null;

            Order order = orders[^1];

            if (order == null)
                return null;

            if (order.State == OrderStateType.Active
                || order.State == OrderStateType.Pending
                || order.State == OrderStateType.None
                || order.State == OrderStateType.Partial)
                return order;
            else if (order.State == OrderStateType.Fail)
            {
                CurrentStatus = ArbitrationStatus.Pause;
            }

            return null;
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

        private void CancelAllLegsOrders()
        {
            CancelActiveOrders(_mainTab);
            for (int i = 0; i < _secondaryTab.Count; i++)
                CancelActiveOrders(_secondaryTab[i]);
        }

        private void CancelActiveOrders(ArbitrationParameters param)
        {
            if (param == null || param.BotTab == null || param.CurrentPosition == null)
                return;

            Order openOrder = GetActiveOpenOrder(param);
            if (openOrder != null && !string.IsNullOrEmpty(openOrder.NumberMarket))
            {
                if (openOrder.IsSendToCancel
                    && openOrder.LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                    return;

                param.BotTab.CloseOrder(openOrder);
                return;
            }

            Order closeOrder = GetActiveCloseOrder(param.CurrentPosition);
            if (closeOrder != null && !string.IsNullOrEmpty(closeOrder.NumberMarket))
            {
                if (closeOrder.IsSendToCancel
                    && closeOrder.LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                    return;

                param.BotTab.CloseOrder(closeOrder);
            }
        }

        private bool IsLastOpenOrderTerminal(Position pos)
        {
            if (pos == null || pos.OpenOrders == null || pos.OpenOrders.Count == 0)
                return true;

            Order lastOrder = pos.OpenOrders[pos.OpenOrders.Count - 1];
            if (lastOrder == null)
                return true;

            return lastOrder.State == OrderStateType.Done
                || lastOrder.State == OrderStateType.Cancel
                || lastOrder.State == OrderStateType.Fail;
        }

        private bool IsLastCloseOrderTerminal(Position pos)
        {
            if (pos == null || pos.CloseOrders == null || pos.CloseOrders.Count == 0)
                return true;

            Order lastOrder = pos.CloseOrders[pos.CloseOrders.Count - 1];
            if (lastOrder == null)
                return true;

            return lastOrder.State == OrderStateType.Done
                || lastOrder.State == OrderStateType.Cancel
                || lastOrder.State == OrderStateType.Fail;
        }

        private bool IsPositionDone(Position pos)
        {
            if (pos == null)
                return true;

            return pos.State == PositionStateType.Done
                || pos.State == PositionStateType.OpeningFail;
        }

        private bool ShouldClearPosition(ArbitrationParameters param)
        {
            Position pos = param.CurrentPosition;

            if (pos == null)
            {
                return false;
            }

            if (pos.State == PositionStateType.Done)
            {
                return true;
            }

            if (pos.State == PositionStateType.OpeningFail)
            {
                bool isOpenMode = CurrentMode == ArbitrationMode.OpenBuyFirstSellSecond
                    || CurrentMode == ArbitrationMode.OpenSellFirstBuySecond;

                if (isOpenMode && !IsTargetVolumeFilled(param))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        #endregion

        #region Step-based helpers

        private int GetCurrentOpenStepIndex()
        {
            int maxSteps = GetMaxEnterStepCount();

            for (int stepIndex = 0; stepIndex < maxSteps; stepIndex++)
            {
                if (!IsEnterStepComplete(_mainTab, stepIndex))
                    return stepIndex;

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    if (!IsEnterStepComplete(_secondaryTab[i], stepIndex))
                        return stepIndex;
                }
            }

            return -1;
        }

        private int GetCurrentCloseStepIndex()
        {
            int maxSteps = GetMaxExitStepCount();

            for (int stepIndex = 0; stepIndex < maxSteps; stepIndex++)
            {
                if (!IsExitStepComplete(_mainTab, stepIndex))
                    return stepIndex;

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    if (!IsExitStepComplete(_secondaryTab[i], stepIndex))
                        return stepIndex;
                }
            }

            return -1;
        }

        private int GetMaxEnterStepCount()
        {
            int max = 0;

            if (_mainTab.EnterArbitrationSteps != null)
                max = _mainTab.EnterArbitrationSteps.Count;

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                if (_secondaryTab[i].EnterArbitrationSteps != null
                    && _secondaryTab[i].EnterArbitrationSteps.Count > max)
                    max = _secondaryTab[i].EnterArbitrationSteps.Count;
            }

            return max;
        }

        private int GetMaxExitStepCount()
        {
            int max = 0;

            if (_mainTab.ExitArbitrationSteps != null)
                max = _mainTab.ExitArbitrationSteps.Count;

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                if (_secondaryTab[i].ExitArbitrationSteps != null
                    && _secondaryTab[i].ExitArbitrationSteps.Count > max)
                    max = _secondaryTab[i].ExitArbitrationSteps.Count;
            }

            return max;
        }

        private bool IsEnterStepComplete(ArbitrationParameters param, int stepIndex)
        {
            if (param.EnterArbitrationSteps == null || stepIndex >= param.EnterArbitrationSteps.Count)
                return true;

            decimal filled = param.CurrentPosition != null ? param.CurrentPosition.OpenVolume : 0m;
            decimal target = GetCumulativeEnterVolume(param, stepIndex);

            return filled >= target;
        }

        private bool IsExitStepComplete(ArbitrationParameters param, int stepIndex)
        {
            if (param.ExitArbitrationSteps == null || stepIndex >= param.ExitArbitrationSteps.Count)
                return true;

            decimal totalVolume = GetTotalVolume(param);
            decimal currentOpenVolume = 0m;

            if (param.CurrentPosition != null && !IsPositionDone(param.CurrentPosition))
                currentOpenVolume = param.CurrentPosition.OpenVolume;

            decimal closedVolume = totalVolume - currentOpenVolume;
            decimal target = GetCumulativeExitVolume(param, stepIndex);

            return closedVolume >= target;
        }

        private decimal GetCumulativeEnterVolume(ArbitrationParameters param, int stepIndex)
        {
            if (param.EnterArbitrationSteps == null)
                return 0m;

            decimal cumulative = 0m;

            for (int i = 0; i <= stepIndex && i < param.EnterArbitrationSteps.Count; i++)
            {
                cumulative += param.EnterArbitrationSteps[i].VolumeStep;
            }

            return cumulative;
        }

        private decimal GetCumulativeExitVolume(ArbitrationParameters param, int stepIndex)
        {
            if (param.ExitArbitrationSteps == null)
                return 0m;

            decimal cumulative = 0m;

            for (int i = 0; i <= stepIndex && i < param.ExitArbitrationSteps.Count; i++)
            {
                cumulative += param.ExitArbitrationSteps[i].VolumeStep;
            }

            return cumulative;
        }

        private void UpdateAllEnterStepStatuses()
        {
            UpdateEnterStepStatuses(_mainTab);

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                UpdateEnterStepStatuses(_secondaryTab[i]);
            }
        }

        private void UpdateAllExitStepStatuses()
        {
            UpdateExitStepStatuses(_mainTab);

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                UpdateExitStepStatuses(_secondaryTab[i]);
            }
        }

        private void UpdateEnterStepStatuses(ArbitrationParameters param)
        {
            if (param.EnterArbitrationSteps == null)
                return;

            decimal filled = param.CurrentPosition != null ? param.CurrentPosition.OpenVolume : 0m;
            decimal cumulative = 0m;

            for (int i = 0; i < param.EnterArbitrationSteps.Count; i++)
            {
                decimal previousCumulative = cumulative;
                cumulative += param.EnterArbitrationSteps[i].VolumeStep;

                if (filled >= cumulative)
                {
                    param.EnterArbitrationSteps[i].Status = OrderStateType.Done;
                    param.EnterArbitrationSteps[i].OpenVolume = param.EnterArbitrationSteps[i].VolumeStep;
                }
                else if (filled > previousCumulative)
                {
                    param.EnterArbitrationSteps[i].Status = OrderStateType.Partial;
                    param.EnterArbitrationSteps[i].OpenVolume = filled - previousCumulative;
                }
                else
                {
                    param.EnterArbitrationSteps[i].Status = OrderStateType.None;
                    param.EnterArbitrationSteps[i].OpenVolume = 0m;
                }
            }
        }

        private void UpdateExitStepStatuses(ArbitrationParameters param)
        {
            if (param.ExitArbitrationSteps == null)
                return;

            decimal totalVolume = GetTotalVolume(param);
            decimal currentOpenVolume = 0m;

            if (param.CurrentPosition != null && !IsPositionDone(param.CurrentPosition))
                currentOpenVolume = param.CurrentPosition.OpenVolume;

            decimal closedVolume = totalVolume - currentOpenVolume;
            decimal cumulative = 0m;

            for (int i = 0; i < param.ExitArbitrationSteps.Count; i++)
            {
                decimal previousCumulative = cumulative;
                cumulative += param.ExitArbitrationSteps[i].VolumeStep;

                if (closedVolume >= cumulative)
                {
                    param.ExitArbitrationSteps[i].Status = OrderStateType.Done;
                    param.ExitArbitrationSteps[i].OpenVolume = param.ExitArbitrationSteps[i].VolumeStep;
                }
                else if (closedVolume > previousCumulative)
                {
                    param.ExitArbitrationSteps[i].Status = OrderStateType.Partial;
                    param.ExitArbitrationSteps[i].OpenVolume = closedVolume - previousCumulative;
                }
                else
                {
                    param.ExitArbitrationSteps[i].Status = OrderStateType.None;
                    param.ExitArbitrationSteps[i].OpenVolume = 0m;
                }
            }
        }

        #endregion

        #region Step logging

        private void LogEnterStepTransitions(int currentStepIndex)
        {
            if (currentStepIndex == _lastLoggedEnterStepIndex)
                return;

            int totalSteps = GetMaxEnterStepCount();

            if (_lastLoggedEnterStepIndex >= 0)
            {
                int completedStepNumber = _lastLoggedEnterStepIndex + 1;

                string message = "Вход. Шаг " + completedStepNumber + " из " + totalSteps + " завершён";

                message += BuildEnterStepDetails(_mainTab, _lastLoggedEnterStepIndex, "База");

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    message += BuildEnterStepDetails(_secondaryTab[i], _lastLoggedEnterStepIndex, "Фьючерс[" + i + "]");
                }

                InfoLogEvent?.Invoke(message);
            }

            if (currentStepIndex >= 0)
            {
                int newStepNumber = currentStepIndex + 1;

                string message = "Вход. Начало шага " + newStepNumber + " из " + totalSteps;

                decimal mainFilled = _mainTab.CurrentPosition != null ? _mainTab.CurrentPosition.OpenVolume : 0m;
                decimal mainTarget = GetCumulativeEnterVolume(_mainTab, currentStepIndex);
                decimal mainStepVolume = GetEnterStepVolume(_mainTab, currentStepIndex);

                message += "\n  База: объём шага = " + mainStepVolume
                    + ", целевой кумулятивный = " + mainTarget
                    + ", текущий заполненный = " + mainFilled;

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    decimal secFilled = _secondaryTab[i].CurrentPosition != null ? _secondaryTab[i].CurrentPosition.OpenVolume : 0m;
                    decimal secTarget = GetCumulativeEnterVolume(_secondaryTab[i], currentStepIndex);
                    decimal secStepVolume = GetEnterStepVolume(_secondaryTab[i], currentStepIndex);

                    message += "\n  Фьючерс[" + i + "]: объём шага = " + secStepVolume
                        + ", целевой кумулятивный = " + secTarget
                        + ", текущий заполненный = " + secFilled;
                }

                InfoLogEvent?.Invoke(message);
            }
            else if (_lastLoggedEnterStepIndex >= 0)
            {
                InfoLogEvent?.Invoke("Вход. Все шаги завершены (" + totalSteps + " из " + totalSteps + ")");
            }

            _lastLoggedEnterStepIndex = currentStepIndex;
        }

        private string BuildEnterStepDetails(ArbitrationParameters param, int stepIndex, string label)
        {
            decimal stepVolume = GetEnterStepVolume(param, stepIndex);
            decimal filled = param.CurrentPosition != null ? param.CurrentPosition.OpenVolume : 0m;
            decimal cumulativeTarget = GetCumulativeEnterVolume(param, stepIndex);
            decimal stepFilled = filled - (cumulativeTarget - stepVolume);

            if (stepFilled < 0m)
                stepFilled = 0m;

            if (stepFilled > stepVolume)
                stepFilled = stepVolume;

            return "\n  " + label + ": объём шага = " + stepVolume + ", заполнено = " + stepFilled;
        }

        private decimal GetEnterStepVolume(ArbitrationParameters param, int stepIndex)
        {
            if (param.EnterArbitrationSteps == null || stepIndex >= param.EnterArbitrationSteps.Count)
                return 0m;

            return param.EnterArbitrationSteps[stepIndex].VolumeStep;
        }

        private void LogExitStepTransitions(int currentStepIndex)
        {
            if (currentStepIndex == _lastLoggedExitStepIndex)
                return;

            int totalSteps = GetMaxExitStepCount();

            if (_lastLoggedExitStepIndex >= 0)
            {
                int completedStepNumber = _lastLoggedExitStepIndex + 1;

                string message = "Выход. Шаг " + completedStepNumber + " из " + totalSteps + " завершён";

                message += BuildExitStepDetails(_mainTab, _lastLoggedExitStepIndex, "База");

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    message += BuildExitStepDetails(_secondaryTab[i], _lastLoggedExitStepIndex, "Фьючерс[" + i + "]");
                }

                InfoLogEvent?.Invoke(message);
            }

            if (currentStepIndex >= 0)
            {
                int newStepNumber = currentStepIndex + 1;

                string message = "Выход. Начало шага " + newStepNumber + " из " + totalSteps;

                decimal mainTotalVolume = GetTotalVolume(_mainTab);
                decimal mainOpenVolume = (_mainTab.CurrentPosition != null && !IsPositionDone(_mainTab.CurrentPosition))
                    ? _mainTab.CurrentPosition.OpenVolume : 0m;
                decimal mainClosed = mainTotalVolume - mainOpenVolume;
                decimal mainTarget = GetCumulativeExitVolume(_mainTab, currentStepIndex);
                decimal mainStepVolume = GetExitStepVolume(_mainTab, currentStepIndex);

                message += "\n  База: объём шага = " + mainStepVolume
                    + ", целевой кумулятивный = " + mainTarget
                    + ", текущий закрытый = " + mainClosed;

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    decimal secTotalVolume = GetTotalVolume(_secondaryTab[i]);
                    decimal secOpenVolume = (_secondaryTab[i].CurrentPosition != null && !IsPositionDone(_secondaryTab[i].CurrentPosition))
                        ? _secondaryTab[i].CurrentPosition.OpenVolume : 0m;
                    decimal secClosed = secTotalVolume - secOpenVolume;
                    decimal secTarget = GetCumulativeExitVolume(_secondaryTab[i], currentStepIndex);
                    decimal secStepVolume = GetExitStepVolume(_secondaryTab[i], currentStepIndex);

                    message += "\n  Фьючерс[" + i + "]: объём шага = " + secStepVolume
                        + ", целевой кумулятивный = " + secTarget
                        + ", текущий закрытый = " + secClosed;
                }

                InfoLogEvent?.Invoke(message);
            }
            else if (_lastLoggedExitStepIndex >= 0)
            {
                InfoLogEvent?.Invoke("Выход. Все шаги завершены (" + totalSteps + " из " + totalSteps + ")");
            }

            _lastLoggedExitStepIndex = currentStepIndex;
        }

        private string BuildExitStepDetails(ArbitrationParameters param, int stepIndex, string label)
        {
            decimal stepVolume = GetExitStepVolume(param, stepIndex);
            decimal totalVolume = GetTotalVolume(param);
            decimal openVolume = (param.CurrentPosition != null && !IsPositionDone(param.CurrentPosition))
                ? param.CurrentPosition.OpenVolume : 0m;
            decimal closedVolume = totalVolume - openVolume;
            decimal cumulativeTarget = GetCumulativeExitVolume(param, stepIndex);
            decimal stepClosed = closedVolume - (cumulativeTarget - stepVolume);

            if (stepClosed < 0m)
                stepClosed = 0m;

            if (stepClosed > stepVolume)
                stepClosed = stepVolume;

            return "\n  " + label + ": объём шага = " + stepVolume + ", закрыто = " + stepClosed;
        }

        private decimal GetExitStepVolume(ArbitrationParameters param, int stepIndex)
        {
            if (param.ExitArbitrationSteps == null || stepIndex >= param.ExitArbitrationSteps.Count)
                return 0m;

            return param.ExitArbitrationSteps[stepIndex].VolumeStep;
        }

        #endregion

        #region Helpers

        private void CalculateIcebergSteps()
        {
            try
            {
                CalculateStepsForParameter(_mainTab);

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    CalculateStepsForParameter(_secondaryTab[i]);
                }

                string message = "Айсберг модуль"
                    + "\nБаза: Вход за " + _mainTab.EnterArbitrationSteps.Count + " шагов."
                    + " Общий объём(лоты): " + SumStepVolumes(_mainTab.EnterArbitrationSteps).ToStringWithNoEndZero()
                    + ", объём шага: " + GetFirstStepVolume(_mainTab.EnterArbitrationSteps).ToStringWithNoEndZero()
                    + "\nБаза: Выход за " + _mainTab.ExitArbitrationSteps.Count + " шагов."
                    + " Общий объём(лоты): " + SumStepVolumes(_mainTab.ExitArbitrationSteps).ToStringWithNoEndZero()
                    + ", объём шага: " + GetFirstStepVolume(_mainTab.ExitArbitrationSteps).ToStringWithNoEndZero();

                message += "\n ---- \n";

                for (int i = 0; i < _secondaryTab.Count; i++)
                {
                    message += "Фьючерс:[" + i + "]: Вход за " + _secondaryTab[i].EnterArbitrationSteps.Count + " шагов."
                        + " Общий объём(лоты): " + SumStepVolumes(_secondaryTab[i].EnterArbitrationSteps).ToStringWithNoEndZero()
                        + ", объём шага: " + GetFirstStepVolume(_secondaryTab[i].EnterArbitrationSteps).ToStringWithNoEndZero()
                        + "\nФьючерс:[" + i + "]: Выход за " + _secondaryTab[i].ExitArbitrationSteps.Count + " шагов."
                        + " Общий объём(лоты): " + SumStepVolumes(_secondaryTab[i].ExitArbitrationSteps).ToStringWithNoEndZero()
                        + ", объём шага: " + GetFirstStepVolume(_secondaryTab[i].ExitArbitrationSteps).ToStringWithNoEndZero();
                }

                InfoLogEvent?.Invoke(message);
            }
            catch (Exception ex)
            {
                InfoLogEvent?.Invoke(ex.ToString());
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CalculateStepsForParameter(ArbitrationParameters param)
        {
            decimal totalVolume = GetTotalVolume(param);
            decimal enterStepVolume = GetEnterVolume(param);
            decimal exitStepVolume = GetExitVolume(param);

            param.EnterArbitrationSteps = CreateSteps(totalVolume, enterStepVolume);
            param.ExitArbitrationSteps = CreateSteps(totalVolume, exitStepVolume);
        }

        private List<ArbitrationStep> CreateSteps(decimal totalVolume, decimal stepVolume)
        {
            List<ArbitrationStep> steps = new List<ArbitrationStep>();

            if (totalVolume <= 0 || stepVolume <= 0)
            {
                return steps;
            }

            decimal remainingVolume = totalVolume;
            int stepNumber = 1;

            while (remainingVolume > 0)
            {
                decimal currentStepVolume = Math.Min(stepVolume, remainingVolume);

                ArbitrationStep step = new ArbitrationStep();
                step.NumberStep = stepNumber;
                step.VolumeStep = currentStepVolume;
                step.OpenVolume = 0m;
                step.Status = OrderStateType.None;
                step.LastOrder = null;
                step.LastTime = DateTime.MinValue;

                steps.Add(step);

                remainingVolume -= currentStepVolume;
                stepNumber++;
            }

            return steps;
        }

        private decimal SumStepVolumes(List<ArbitrationStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return 0;
            }

            decimal sum = 0;

            for (int i = 0; i < steps.Count; i++)
            {
                sum += steps[i].VolumeStep;
            }

            return sum;
        }

        private decimal GetFirstStepVolume(List<ArbitrationStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return 0;
            }

            return steps[0].VolumeStep;
        }

        public void Reconnect()
        {
            if (_savedMainCurrentPositionNumber >= 0 && _mainTab.BotTab != null)
            {
                Position pos = FindPositionByNumber(_mainTab.BotTab, _savedMainCurrentPositionNumber);
                if (pos != null && !IsPositionDone(pos))
                    _mainTab.CurrentPosition = pos;
            }
            _savedMainCurrentPositionNumber = -1;

            for (int i = 0; i < _savedSecondaryCurrentPositionNumbers.Count && i < _secondaryTab.Count; i++)
            {
                if (_secondaryTab[i].BotTab == null)
                    continue;

                int num = _savedSecondaryCurrentPositionNumbers[i];
                if (num >= 0)
                {
                    Position pos = FindPositionByNumber(_secondaryTab[i].BotTab, num);
                    if (pos != null && !IsPositionDone(pos))
                        _secondaryTab[i].CurrentPosition = pos;
                }
            }
            _savedSecondaryCurrentPositionNumbers.Clear();
        }

        private Position FindPositionByNumber(BotTabSimple tab, int number)
        {
            if (tab == null || tab.PositionsAll == null)
                return null;

            for (int i = 0; i < tab.PositionsAll.Count; i++)
            {
                if (tab.PositionsAll[i].Number == number)
                    return tab.PositionsAll[i];
            }

            return null;
        }

        public bool CheckTradingReady()
        {
            if (!IsLegConnectionReady(_mainTab))
                return false;

            if (_mainTab.BotTab.CandlesAll == null || _mainTab.BotTab.CandlesAll.Count == 0)
                return false;

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                ArbitrationParameters tab = _secondaryTab[i];

                if (!IsLegConnectionReady(tab))
                    return false;

                if (tab.BotTab.CandlesAll == null || tab.BotTab.CandlesAll.Count == 0)
                    return false;
            }

            return true;
        }

        private bool CheckConnectionReady()
        {
            if (!IsLegConnectionReady(_mainTab))
                return false;

            for (int i = 0; i < _secondaryTab.Count; i++)
            {
                if (!IsLegConnectionReady(_secondaryTab[i]))
                    return false;
            }

            return true;
        }

        private bool IsLegConnectionReady(ArbitrationParameters param)
        {
            return param != null
                && param.BotTab != null
                && param.BotTab.ServerStatus == ServerConnectStatus.Connect;
        }

        #endregion
    }
}
