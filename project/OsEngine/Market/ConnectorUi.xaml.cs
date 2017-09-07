/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market
{
    /// <summary>
    /// Логика взаимодействия для ConnectorQuikUi.xaml
    /// </summary>
    public partial class ConnectorUi
    {
        public ConnectorUi(Connector connectorBot)
        {
            try
            {
                InitializeComponent();

                List<IServer> servers = ServerMaster.GetServers();

                if (servers == null)
                {// если сервер для подключения к бирже ещё не создан
                    Close();
                    return;
                }

                // сохраняем коннекторы
                _connectorBot = connectorBot;

                // загружаем настройки в контролы
                for (int i = 0; i < servers.Count; i++)
                {
                    ComboBoxTypeServer.Items.Add(servers[i].ServerType);
                }

                if (connectorBot.ServerType != ServerType.Unknown)
                {
                    ComboBoxTypeServer.SelectedItem = connectorBot.ServerType;
                    _selectedType = connectorBot.ServerType;
                }
                else
                {
                    ComboBoxTypeServer.SelectedItem = servers[0].ServerType;
                    _selectedType = servers[0].ServerType;
                }

                if (ServerMaster.StartProgram == ServerStartProgramm.IsTester)
                {
                    ComboBoxTypeServer.IsEnabled = false;
                    CheckBoxIsEmulator.IsEnabled = false;
                    CheckBoxSetForeign.IsEnabled = false;
                    ComboBoxTypeServer.SelectedItem = ServerType.Tester;
                    ComboBoxClass.SelectedItem = ServerMaster.GetServers()[0].Securities[0].NameClass;
                    ComboBoxPortfolio.SelectedItem = ServerMaster.GetServers()[0].Portfolios[0].Number;

                    connectorBot.ServerType = ServerType.Tester;
                    _selectedType = ServerType.Tester;
                }

                

                LoadClassOnBox();

                LoadSecurityOnBox();

                LoadPortfolioOnBox();

                ComboBoxClass.SelectionChanged += ComboBoxClass_SelectionChanged;

                CheckBoxIsEmulator.IsChecked = _connectorBot.EmulatorIsOn;

                CheckBoxSetForeign.IsChecked = _connectorBot.SetForeign;

                ComboBoxTypeServer.SelectionChanged += ComboBoxTypeServer_SelectionChanged;

                BoxTimeFrame.SelectionChanged += BoxTimeFrame_SelectionChanged;

                CreateTimeFrameBox();

                BoxTimeFrame.ToolTip = "ТФ Delta не имеет чёткого критерия закрытия и закрывает свечи по изменению дельты \n(разницы между объёмом текущих покупок и продаж прошедших с утра) на N(настраивается отдельно) пунктов";
                
                

                BoxTimeCandleCreateType.Items.Add(CandleSeriesCreateDataType.Tick);
                BoxTimeCandleCreateType.Items.Add(CandleSeriesCreateDataType.MarketDepth);
                BoxTimeCandleCreateType.SelectedItem = _connectorBot.CandleCreateType;
                BoxTimeCandleCreateType.SelectionChanged += BoxTimeCandleCreateType_SelectionChanged;
                TextBoxCountTradesInCandle.Text = _connectorBot.CountTradeInCandle.ToString();
                TextBoxCountTradesInCandle.TextChanged += TextBoxCountTradesInCandle_TextChanged;
            }
            catch (Exception error)
            {
                 MessageBox.Show("Ошибка в конструкторе " + error);
            }
        }

        void BoxTimeCandleCreateType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
             CreateTimeFrameBox();
        }

        private void CreateTimeFrameBox()
        {
            BoxTimeFrame.Items.Clear();

            if (ServerMaster.StartProgram == ServerStartProgramm.IsTester)
            {
                // таймФрейм
                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];
                if (server.TypeTesterData != TesterDataType.Candle)
                {
                    // если строим данные на тиках или стаканах, то можно использовать любой ТФ
                    // менеджер свечей построит любой
                    BoxTimeFrame.Items.Add(TimeFrame.Day);
                    BoxTimeFrame.Items.Add(TimeFrame.Hour2);
                    BoxTimeFrame.Items.Add(TimeFrame.Hour1);
                    BoxTimeFrame.Items.Add(TimeFrame.Min30);
                    BoxTimeFrame.Items.Add(TimeFrame.Min20);
                    BoxTimeFrame.Items.Add(TimeFrame.Min15);
                    BoxTimeFrame.Items.Add(TimeFrame.Min10);
                    BoxTimeFrame.Items.Add(TimeFrame.Min5);
                    BoxTimeFrame.Items.Add(TimeFrame.Min2);
                    BoxTimeFrame.Items.Add(TimeFrame.Min1);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec30);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec20);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec15);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec10);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec5);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec2);
                    BoxTimeFrame.Items.Add(TimeFrame.Sec1);

                    BoxTimeFrame.Items.Add(TimeFrame.Delta);
                    BoxTimeFrame.Items.Add(TimeFrame.Tick);
                }
                else
                {
                    // далее, если используем готовые свечки, то нужно ставить только те ТФ, которые есть
                    // и вставляются они только когда мы выбираем бумагу в методе 

                    ComboBoxSecurities.SelectionChanged += ComboBoxSecurities_SelectionChanged;
                    GetTimeFramesInTester();
                }
            }
            else
            {
                BoxTimeFrame.Items.Add(TimeFrame.Day);
                BoxTimeFrame.Items.Add(TimeFrame.Hour2);
                BoxTimeFrame.Items.Add(TimeFrame.Hour1);
                BoxTimeFrame.Items.Add(TimeFrame.Min30);
                BoxTimeFrame.Items.Add(TimeFrame.Min20);
                BoxTimeFrame.Items.Add(TimeFrame.Min15);
                BoxTimeFrame.Items.Add(TimeFrame.Min10);
                BoxTimeFrame.Items.Add(TimeFrame.Min5);
                BoxTimeFrame.Items.Add(TimeFrame.Min2);
                BoxTimeFrame.Items.Add(TimeFrame.Min1);
                BoxTimeFrame.Items.Add(TimeFrame.Sec30);
                BoxTimeFrame.Items.Add(TimeFrame.Sec20);
                BoxTimeFrame.Items.Add(TimeFrame.Sec15);
                BoxTimeFrame.Items.Add(TimeFrame.Sec10);
                BoxTimeFrame.Items.Add(TimeFrame.Sec5);
                BoxTimeFrame.Items.Add(TimeFrame.Sec2);
                BoxTimeFrame.Items.Add(TimeFrame.Sec1);

                CandleSeriesCreateDataType createType = CandleSeriesCreateDataType.Tick;
                if (BoxTimeCandleCreateType.SelectedItem != null)
                {
                    Enum.TryParse(BoxTimeCandleCreateType.SelectedItem.ToString(), true, out createType);
                }
                

                if (createType == CandleSeriesCreateDataType.Tick)
                {
                    BoxTimeFrame.Items.Add(TimeFrame.Tick);
                    BoxTimeFrame.Items.Add(TimeFrame.Delta);
                }
            }

            BoxTimeFrame.SelectedItem = _connectorBot.TimeFrame;

            if (BoxTimeFrame.SelectedItem == null)
            {
                BoxTimeFrame.SelectedItem = TimeFrame.Min1;
            }
        }

        /// <summary>
        /// изменилось кол-во трейдов в свече с ТФ "трейды"
        /// </summary>
        void TextBoxCountTradesInCandle_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxCountTradesInCandle.Text) <= 0)
                {
                    throw new Exception();
                }
                _connectorBot.CountTradeInCandle = Convert.ToInt32(TextBoxCountTradesInCandle.Text);
            }
            catch
            {
                TextBoxCountTradesInCandle.Text = _connectorBot.CountTradeInCandle.ToString();
            }
        }

        /// <summary>
        /// переключен таймфрейм
        /// </summary>
        void BoxTimeFrame_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (BoxTimeFrame.SelectedValue == null)
            {
                return;
            }
            if (BoxTimeFrame.SelectedValue.ToString() == "Delta")
            {
                BoxTimeFrame.Margin = new Thickness(189, 130, 99, 0);
                ButtonDeltaSettings.Margin = new Thickness(302, 130, 24, 0);
                TextBoxCountTradesInCandle.Margin = new Thickness(302, 930, 24, 0);
            }
            else if (BoxTimeFrame.SelectedValue.ToString() == "Tick")
            {
                BoxTimeFrame.Margin = new Thickness(189, 130, 99, 0);
                ButtonDeltaSettings.Margin = new Thickness(302, 930, 24, 0);
                TextBoxCountTradesInCandle.Margin = new Thickness(302, 131, 24, 0);
            }
            else
            {
                BoxTimeFrame.Margin = new Thickness(189, 130, 24, 0);
                ButtonDeltaSettings.Margin = new Thickness(302, 930, 24, 0);
                TextBoxCountTradesInCandle.Margin = new Thickness(302, 930, 24, 0);
            }
        }

        /// <summary>
        /// запрашиваем настройки для дельты
        /// </summary>
        private void ButtonDeltaSettings_Click(object sender, RoutedEventArgs e)
        {
            _connectorBot.TimeFrameBuilder.DeltaPeriods.ShowDialog();
            _connectorBot.Save();
        }

        /// <summary>
        /// нужно когда изменяется бумага. При тестовом подключении 
        /// смотрим здесь ТайФреймы для этой бумаги
        /// </summary>
        void ComboBoxSecurities_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            GetTimeFramesInTester();
        }

        private void GetTimeFramesInTester()
        {
            if (ComboBoxSecurities.SelectedItem == null)
            {
                return;
            }
            TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

            if (server.TypeTesterData != TesterDataType.Candle)
            {
                return;
            }

            List<SecurityTester> securities = server.SecuritiesTester;

            string name = ComboBoxSecurities.SelectedItem.ToString();

            string lastTf = null;

            if (BoxTimeFrame.SelectedItem != null)
            {
                lastTf = BoxTimeFrame.SelectedItem.ToString();
            }

            BoxTimeFrame.Items.Clear();

            for (int i = 0; i < securities.Count; i++)
            {
                if (name == securities[i].Security.Name)
                {
                    BoxTimeFrame.Items.Add(securities[i].TimeFrame);
                }
            }
            if (lastTf == null)
            {
                BoxTimeFrame.SelectedItem = securities[0].TimeFrame;
            }
            else
            {
                TimeFrame oldFrame;
                Enum.TryParse(lastTf, out oldFrame);

                BoxTimeFrame.SelectedItem = oldFrame;
            }
        }

        /// <summary>
        /// коннектор к серверу
        /// </summary>
        private Connector _connectorBot;

        /// <summary>
        /// выбранный в данным момент сервер
        /// </summary>
        private ServerType _selectedType;

        /// <summary>
        /// пользователь изменил тип сервера для подключения
        /// </summary>
        void ComboBoxTypeServer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server != null)
                {
                    server.SecuritiesChangeEvent -= server_SecuritiesCharngeEvent;
                    server.PortfoliosChangeEvent -= server_PortfoliosChangeEvent;
                }

                Enum.TryParse(ComboBoxTypeServer.SelectedItem.ToString(), true, out _selectedType);

                IServer server2 = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server2 != null)
                {
                    server2.SecuritiesChangeEvent += server_SecuritiesCharngeEvent;
                    server2.PortfoliosChangeEvent += server_PortfoliosChangeEvent;
                }
                LoadPortfolioOnBox();
                LoadClassOnBox();
                LoadSecurityOnBox();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// происходит после переключения класса отображаемых инструментов
        /// </summary>
        void ComboBoxClass_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSecurityOnBox();
        }

        /// <summary>
        /// на сервер пришли новые бумаги
        /// </summary>
        void server_SecuritiesCharngeEvent(List<Security> securities)
        {
            LoadClassOnBox();
        }

        /// <summary>
        /// на сервер пришли новые счета
        /// </summary>
        void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            LoadPortfolioOnBox();
        }

        /// <summary>
        /// выгружает счета на форму
        /// </summary>
        private void LoadPortfolioOnBox() 
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server == null)
                {
                    return;
                }


                if (!ComboBoxClass.CheckAccess())
                {
                    ComboBoxClass.Dispatcher.Invoke(LoadPortfolioOnBox);
                    return;
                }

                string curPortfolio = null;

                if (ComboBoxPortfolio.SelectedItem != null)
                {
                    curPortfolio = ComboBoxPortfolio.SelectedItem.ToString();
                }

                ComboBoxPortfolio.Items.Clear();


                string portfolio = _connectorBot.PortfolioName;


                if (portfolio != null)
                {
                    ComboBoxPortfolio.Items.Add(_connectorBot.PortfolioName);
                    ComboBoxPortfolio.Text = _connectorBot.PortfolioName;
                }

                List<Portfolio> portfolios = server.Portfolios;

                if (portfolios == null)
                {
                    return;
                }

                for (int i = 0; i < portfolios.Count; i++)
                {
                    bool isInArray = false;

                    for (int i2 = 0; i2 < ComboBoxPortfolio.Items.Count; i2++)
                    {
                        if (ComboBoxPortfolio.Items[i2].ToString() == portfolios[i].Number)
                        {
                            isInArray = true;
                        }
                    }

                    if (isInArray == true)
                    {
                        continue;
                    }
                    ComboBoxPortfolio.Items.Add(portfolios[i].Number);
                }
                if (curPortfolio != null)
                {
                    for (int i = 0; i < ComboBoxPortfolio.Items.Count; i++)
                    {
                        if (ComboBoxPortfolio.Items[i].ToString() == curPortfolio)
                        {
                            ComboBoxPortfolio.SelectedItem = curPortfolio;
                            break;
                        }
                    }
                   
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// поместить классы в окно
        /// </summary>
        private void LoadClassOnBox()
        {
            try
            {
                if (!ComboBoxClass.Dispatcher.CheckAccess())
                {
                    ComboBoxClass.Dispatcher.Invoke(LoadClassOnBox);
                    return;
                }
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server == null)
                {
                    return;
                }

                if (ComboBoxClass.Items.Count != 0)
                {
                    ComboBoxClass.Items.Clear();
                }

                var securities = server.Securities;

                ComboBoxClass.Items.Clear();

                if (securities == null)
                {
                    return;
                }

                for (int i1 = 0; i1 < securities.Count; i1++)
                {
                    string clas = securities[i1].NameClass;

                    if (ComboBoxClass.Items.Count == 0)
                    {
                        ComboBoxClass.Items.Add(clas);
                        continue;
                    }

                    bool isInArray = false;

                    for (int i = 0; i < ComboBoxClass.Items.Count; i++)
                    {
                        string item = ComboBoxClass.Items[i].ToString();
                        if (item == clas)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        ComboBoxClass.Items.Add(clas);
                    }
                }
                if (_connectorBot.Security != null)
                {
                    ComboBoxClass.SelectedItem = _connectorBot.Security.NameClass;
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// выгрузить данные из хранилища на форму
        /// </summary>
        private void LoadSecurityOnBox()
        {
            try
            {
                List<IServer> serversAll = ServerMaster.GetServers();

                IServer server = serversAll.Find(server1 => server1.ServerType == _selectedType);

                if (server == null)
                {
                    return;
                }
                // стираем всё

                ComboBoxSecurities.Items.Clear();
                // грузим инструменты доступные для скачивания

                var securities = server.Securities;

                if (securities != null)
                {
                    for (int i = 0; i < securities.Count; i++)
                    {
                        string classSec = securities[i].NameClass;
                        if (ComboBoxClass.SelectedItem != null && classSec == ComboBoxClass.SelectedItem.ToString())
                        {
                            ComboBoxSecurities.Items.Add(securities[i].Name);
                            ComboBoxSecurities.SelectedItem = securities[i];
                        }
                    }
                }

                // грузим уже запущенные инструменты

                string paper = _connectorBot.NamePaper;

                if (paper != null)
                {
                    ComboBoxSecurities.Text = paper;

                    if (ComboBoxSecurities.Text == null)
                    {
                        ComboBoxSecurities.Items.Add(_connectorBot.NamePaper);
                        ComboBoxSecurities.Text = _connectorBot.NamePaper;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        ///  кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _connectorBot.PortfolioName = ComboBoxPortfolio.Text;

                if (CheckBoxIsEmulator.IsChecked != null)
                {
                    _connectorBot.EmulatorIsOn = CheckBoxIsEmulator.IsChecked.Value;
                }

                TimeFrame timeFrame;

                Enum.TryParse(BoxTimeFrame.Text, out timeFrame);

                _connectorBot.TimeFrame = timeFrame;

                _connectorBot.NamePaper = ComboBoxSecurities.Text;

                Enum.TryParse(ComboBoxTypeServer.Text, true, out _connectorBot.ServerType);

                CandleSeriesCreateDataType createType;
                Enum.TryParse(BoxTimeCandleCreateType.Text, true, out createType);

                _connectorBot.CandleCreateType = createType;

                if (CheckBoxSetForeign.IsChecked.HasValue)
                {
                    _connectorBot.SetForeign = CheckBoxSetForeign.IsChecked.Value;
                }
                

                _connectorBot.Save();
                Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // сообщения в лог 

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
