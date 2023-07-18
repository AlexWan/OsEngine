/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Market.Servers;
using OsEngine.Charts.CandleChart;
using System;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots
{
    /// <summary>
    /// Робот созданный для тестирования синхронности свечек созданных внутри OsEngine и запрошенных с биржи
    /// </summary>
    [Bot("TestBotCandlesComparison")]
    public class TestBotCandlesComparison : BotPanel
    {
        public TestBotCandlesComparison(string name, StartProgram startProgram) : base(name, startProgram)
        {

            if (startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            StrategyParameterButton button = CreateParameterButton("Compare candles OHLC");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            StrategyParameterButton button2 = CreateParameterButton("Compare candles Volume");
            button2.UserClickOnButtonEvent += ButtonVolume_UserClickOnButtonEvent;

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Description = "Do not turn on - a robot for testing the synchronism of " +
                "candles created inside OsEngine and requested from the exchange";
        }

        BotTabSimple _tab;

        public override string GetNameStrategyType()
        {
            return "TestBotCandlesComparison";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private DateTime _lastClickTime;

        private void Button_UserClickOnButtonEvent()
        {
            if (_uiOsEngineCandles != null ||
                _uiOsServerCandles != null)
            {
                _tab.SetNewLogMessage("Предыдущее окно ещё не закрыто!!!", Logging.LogMessageType.Error);
                return;
            }

            if (_lastClickTime.AddSeconds(10)> DateTime.Now)
            {
                return;
            }
            _lastClickTime = DateTime.Now;

            // 1 запрашиваем свечи

            List<Candle> candlesFromOsEngine = _tab.CandlesAll;

            if(candlesFromOsEngine == null 
                || candlesFromOsEngine.Count < 10)
            {
                return;
            }

            IServer server = _tab.Connector.MyServer;

            List<Candle> candlesFromServer
                = server.GetCandleDataToSecurity(
                    _tab.Securiti.Name,
                    _tab.Securiti.NameClass,
                    _tab.TimeFrameBuilder,
                    DateTime.Now.AddDays(-2),
                    DateTime.Now.AddDays(1),
                    DateTime.Now.AddDays(-2),
                    false
                    );

            if (candlesFromServer == null
                || candlesFromServer.Count < 10)
            {
                return;
            }

            // 2 создаём отдельные чарты для свечных графиков

            if(_uiOsEngineCandles == null)
            {
                _uiOsEngineCandles = new CandleChartUi("myCandlesTestBot", this.StartProgram);
                _uiOsEngineCandles.Show();
                _uiOsEngineCandles.ChangeTitle("Candles from OsEngine");
                _uiOsEngineCandles.Closed += _uiOsEngineCandles_Closed;
            }
            else
            {
                _uiOsEngineCandles.Activate();
            }

            _uiOsEngineCandles.ProcessCandles(candlesFromOsEngine);

            if(_uiOsServerCandles == null)
            {
                _uiOsServerCandles = new CandleChartUi("serverCandlesTestBot", this.StartProgram);
                _uiOsServerCandles.Show();
                _uiOsServerCandles.ChangeTitle("Candles from Server");
                _uiOsServerCandles.Closed += _uiOsServerCandles_Closed;
            }
            else
            {
                _uiOsServerCandles.Activate();
            }

            _uiOsServerCandles.ProcessCandles(candlesFromServer);

            _osCandles = candlesFromOsEngine;
            _servCandles = candlesFromServer;

            Thread worker = new Thread(RePaintDiffCandles);
            worker.Start();

        }

        List<Candle> _osCandles;
        List<Candle> _servCandles;

        private void RePaintDiffCandles()
        {
            Thread.Sleep(5000);

            // 3 раскрашиваем не совпадающие свечи

            for (int indexMyC = 0, indexServC = 0; indexMyC < _osCandles.Count && indexServC < _servCandles.Count;)
            {
                Candle osCandle = _osCandles[indexMyC];
                Candle servCandle = _servCandles[indexServC];

                if(indexMyC +1 == _osCandles.Count)
                {

                }

                if (osCandle.TimeStart < servCandle.TimeStart)
                {
                    indexMyC++;
                    continue;
                }

                if (servCandle.TimeStart < osCandle.TimeStart)
                {
                    indexServC++;
                    continue;
                }

                if (osCandle.High != servCandle.High
                    || osCandle.Low != servCandle.Low
                    || osCandle.Open != servCandle.Open
                    || osCandle.Close != servCandle.Close)
                {
                    _uiOsEngineCandles.SetColorToCandle(indexMyC, System.Drawing.Color.White);
                    _uiOsServerCandles.SetColorToCandle(indexServC, System.Drawing.Color.White);
                }

                indexMyC++;
                indexServC++;
            }
        }

        private void _uiOsServerCandles_Closed(object sender, EventArgs e)
        {
            _uiOsServerCandles = null;
        }

        private void _uiOsEngineCandles_Closed(object sender, EventArgs e)
        {
            _uiOsEngineCandles = null;
        }

        CandleChartUi _uiOsEngineCandles;

        CandleChartUi _uiOsServerCandles;




        private void ButtonVolume_UserClickOnButtonEvent()
        {
            if(_uiOsEngineCandles != null ||
                _uiOsServerCandles != null)
            {
                _tab.SetNewLogMessage("Предыдущее окно ещё не закрыто!!!", Logging.LogMessageType.Error);
                return;
            }

            if (_lastClickTime.AddSeconds(10) > DateTime.Now)
            {
                return;
            }
            _lastClickTime = DateTime.Now;

            // 1 запрашиваем свечи

            List<Candle> candlesFromOsEngine = _tab.CandlesAll;

            if (candlesFromOsEngine == null
                || candlesFromOsEngine.Count < 10)
            {
                return;
            }

            IServer server = _tab.Connector.MyServer;

            List<Candle> candlesFromServer
                = server.GetCandleDataToSecurity(
                    _tab.Securiti.Name,
                    _tab.Securiti.NameClass,
                    _tab.TimeFrameBuilder,
                    DateTime.Now.AddDays(-2),
                    DateTime.Now.AddDays(1),
                    DateTime.Now.AddDays(-2),
                    false
                    );

            if (candlesFromServer == null
                || candlesFromServer.Count < 10)
            {
                return;
            }

            // 2 создаём отдельные чарты для свечных графиков

            if (_uiOsEngineCandles == null)
            {
                _uiOsEngineCandles = new CandleChartUi("myCandlesTestBot", this.StartProgram);
                _uiOsEngineCandles.Show();
                _uiOsEngineCandles.ChangeTitle("Candles from OsEngine");
                _uiOsEngineCandles.Closed += _uiOsEngineCandles_Closed;
            }
            else
            {
                _uiOsEngineCandles.Activate();
            }

            _uiOsEngineCandles.ProcessCandles(candlesFromOsEngine);

            if (_uiOsServerCandles == null)
            {
                _uiOsServerCandles = new CandleChartUi("serverCandlesTestBot", this.StartProgram);
                _uiOsServerCandles.Show();
                _uiOsServerCandles.ChangeTitle("Candles from Server");
                _uiOsServerCandles.Closed += _uiOsServerCandles_Closed;
            }
            else
            {
                _uiOsServerCandles.Activate();
            }

            _uiOsServerCandles.ProcessCandles(candlesFromServer);

            _osCandles = candlesFromOsEngine;
            _servCandles = candlesFromServer;

            Thread worker = new Thread(RePaintDiffVolume);
            worker.Start();


        }

        private void RePaintDiffVolume()
        {
            Thread.Sleep(5000);

            // 3 раскрашиваем не совпадающие свечи

            for (int indexMyC = 0, indexServC = 0; indexMyC < _osCandles.Count && indexServC < _servCandles.Count;)
            {
                Candle osCandle = _osCandles[indexMyC];
                Candle servCandle = _servCandles[indexServC];

                if (indexMyC + 1 == _osCandles.Count)
                {

                }

                if (osCandle.TimeStart < servCandle.TimeStart)
                {
                    indexMyC++;
                    continue;
                }

                if (servCandle.TimeStart < osCandle.TimeStart)
                {
                    indexServC++;
                    continue;
                }

                if (osCandle.Volume != servCandle.Volume)
                {
                    _uiOsEngineCandles.SetColorToCandle(indexMyC, System.Drawing.Color.White);
                    _uiOsServerCandles.SetColorToCandle(indexServC, System.Drawing.Color.White);
                }

                indexMyC++;
                indexServC++;
            }
        }


    }
}
