using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;

namespace OsEngine.OsData
{
    public class Benchmark
    {
        private AServer _server;
        private ServerType _serverType = ServerType.None;
        private string _secName;
        private string _secId;
        private string _secClass;
        private string _secNameFull;
        private string _fileSetBenchmark;
        private Series _series;

        public Benchmark(string benchmark)
        {
            if (benchmark == BenchmarkSecurity.BTC.ToString())
            {
                _serverType = ServerType.Finam;
                _secName = "BTC/USD";
                _secId = "1822580";
                _secClass = "Криптовалюты";
                _secNameFull = "XXBTZUSD";
                _fileSetBenchmark = @"Data\Benchmark\BTCUSD\Day\BTCUSD.txt";           
            }

            if (benchmark == BenchmarkSecurity.SnP500.ToString())
            {
                _serverType = ServerType.Finam;
                _secName = "S&P 500 Index";
                _secId = "385008";
                _secClass = "Индексы мировые";
                _secNameFull = "SSPX";
                _fileSetBenchmark = @"Data\Benchmark\S&P 500 Index\Day\S&P 500 Index.txt";
            }

            if (benchmark == BenchmarkSecurity.MCFTR.ToString())
            {
                _serverType = ServerType.Finam;
                _secName = "MCFTR";
                _secId = "465340";
                _secClass = "Российские индексы";
                _secNameFull = "MCFTR";
                _fileSetBenchmark = @"Data\Benchmark\MCFTR\Day\MCFTR.txt";
            }

            if (benchmark == BenchmarkSecurity.IMOEX.ToString())
            {
                _serverType = ServerType.Finam;
                _secName = "Индекс МосБиржи";
                _secId = "420450";
                _secClass = "Индексы Россия";
                _secNameFull = "IMOEX";
                _fileSetBenchmark = @"Data\Benchmark\Индекс МосБиржи\Day\Индекс МосБиржи.txt";
            }
        }

        public string FileSetBenchmark
        {
            get { return _fileSetBenchmark; }             
        }

        public async Task GetData(Series series)
        {
            try
            {
                _series = series;

                if (_serverType == ServerType.None)
                {
                    DownloadBenchmarkEvent();
                    return;
                }

                await Task.Run(async () => await DownloadData());
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return;
            }
        }

        private async Task DownloadData()
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                ServerMaster.CreateServer(_serverType, false);

                List<IServer> serversFromServerMaster = ServerMaster.GetServers();

                if (serversFromServerMaster == null)
                {
                    DownloadBenchmarkEvent();
                    return;
                }

                for (int i = 0; i < serversFromServerMaster.Count; i++)
                {
                    if (serversFromServerMaster[i].ServerType == _serverType)
                    {
                        _server = (AServer)serversFromServerMaster[i];
                    }
                }

                if (_server == null)
                {
                    DownloadBenchmarkEvent();
                    return;
                }

                _server.StartServer();

                SendNewLogMessage("Started downloading benchmark data.", LogMessageType.System);

                while (!cts.Token.IsCancellationRequested) 
                {
                    if (_server.ServerStatus == ServerConnectStatus.Connect)
                    {
                        await Task.Delay(6000, cts.Token).ConfigureAwait(false);

                        DateTime timeStart = DateTime.Parse(_series.Points[0].AxisLabel).AddDays(-30);
                        DateTime timeEnd = DateTime.Parse(_series.Points[^1].AxisLabel).AddDays(1);

                        SettingsToLoadSecurity param = new();

                        param.Regime = DataSetState.On;
                        param.Tf1MinuteIsOn = false;
                        param.Tf2MinuteIsOn = false;
                        param.Tf5MinuteIsOn = false;
                        param.Tf10MinuteIsOn = false;
                        param.Tf15MinuteIsOn = false;
                        param.Tf30MinuteIsOn = false;
                        param.Tf1HourIsOn = false;
                        param.Tf2HourIsOn = false;
                        param.Tf4HourIsOn = false;
                        param.TfDayIsOn = true;
                        param.TfTickIsOn = false;
                        param.TfMarketDepthIsOn = false;
                        param.Source = _server.ServerType;
                        param.TimeStart = timeStart;
                        param.TimeEnd = timeEnd;
                        param.MarketDepthDepth = 5;

                        SecurityToLoad record = new SecurityToLoad();
                        record.SecName = _secName;
                        record.SecId = _secId;
                        record.SecClass = _secClass;
                        record.SecExchange = _server.ServerType.ToString();
                        record.SecNameFull = _secNameFull;
                        record.SetName = "Benchmark";

                        record.CopySettingsFromParam(param);
                        record.NewLogMessageEvent += SendNewLogMessage;

                        record.Process(_server);
                                                
                        SendNewLogMessage("Finished downloading benchmark data.", LogMessageType.System);

                        break;
                    }

                    await Task.Delay(100, cts.Token).ConfigureAwait(false);                                       
                }

                if (cts.Token.IsCancellationRequested)
                {
                    SendNewLogMessage("Donwload benchmark cancelled by timeout", LogMessageType.Error);
                }

                _server.StopServer();

                if (DownloadBenchmarkEvent != null)
                {
                    DownloadBenchmarkEvent();
                }
            }
            catch (OperationCanceledException)
            {
                SendNewLogMessage("Download benchmark cancelled", LogMessageType.Error);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action DownloadBenchmarkEvent;

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion        
    }
    public enum BenchmarkSecurity
    {
        Off,
        BTC,
        MCFTR,
        SnP500,
        IMOEX
    }
}
