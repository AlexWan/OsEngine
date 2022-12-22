using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;


namespace Tinkoff_Router
{
    class Program
    {

        public static string TinkoffToken;

        public static Dictionary<string, ConcurrentQueue<string>> Trades = new Dictionary<string, ConcurrentQueue<string>>();

        public static Dictionary<string, ConcurrentQueue<string>> Depths = new Dictionary<string, ConcurrentQueue<string>>();

        public static Queue<string> Errors = new Queue<string>();

        public static string HandlerResponce(string request)
        {
            if (Trades.ContainsKey(request) == false ||
                Depths.ContainsKey(request) == false)
            {
                Subscrible(request);
            }

            if (Errors.Count != 0)
            {
                return Errors.Dequeue();
            }

            string mes;

            if (Trades[request].Count >= Depths[request].Count && Trades[request].Count != 0)
            {
                Trades[request].TryDequeue(out mes);
                return mes;
            }
            else if (Trades[request].Count < Depths[request].Count && Depths[request].Count != 0)
            {
                Depths[request].TryDequeue(out mes);
                return mes;
            }
            else
            {
                return String.Empty;
            }
        }

        private static void Subscrible(string request)
        {
            Trades[request] = new ConcurrentQueue<string>();
            Depths[request] = new ConcurrentQueue<string>();

            TinkoffDataStreams tinkoff = new TinkoffDataStreams(TinkoffToken);

            new Task(async () =>
            {
                await tinkoff.DataChanelDephts(request);
            }).Start();


            Thread.Sleep(1000);

            new Task(async () =>
            {
                await tinkoff.DataChanelTicks(request);
            }).Start();
        }

        static void Main(string[] args)
        {
            Console.Title = "Tinkoff_Router";

            if (args != null && args.Length != 0)
            {
                TinkoffToken = args[0]; ;
                new Task(() =>
                {
                    SocketServer socketServer = new SocketServer(HandlerResponce);
                    socketServer.StartServer();
                }).Start();
            }
            else
            {

                Console.WriteLine("Args is not found");
                return;
            }

            while (true) { }
        }
    }

    // объект для подписки на бумагу 
    public class TinkoffDataStreams
    {
        private string Token;

        public TinkoffDataStreams(string Token)
        {
            this.Token = Token;
        }

        public async Task DataChanelTicks(string InstId)
        {
            try
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddInvestApiClient(TokenHandler);
                var serviceProvider = serviceCollection.BuildServiceProvider();

                var client = serviceProvider.GetRequiredService<InvestApiClient>();

                var stream = client.MarketDataStream.MarketDataStream();

                await stream.RequestStream.WriteAsync(new MarketDataRequest
                {

                    SubscribeTradesRequest = new SubscribeTradesRequest
                    {
                        Instruments =
                        {
                            new TradeInstrument
                            {
                                Figi = InstId,
                            }
                        },
                        SubscriptionAction = SubscriptionAction.Subscribe
                    }
                });

                await foreach (var response in stream.ResponseStream.ReadAllAsync())
                {
                    if (response.SubscribeTradesResponse != null)
                    {
                        Console.WriteLine("TradesChanel " +
                            response.SubscribeTradesResponse.TradeSubscriptions[0].Figi +
                            ": " +
                            response.SubscribeTradesResponse.TradeSubscriptions[0].SubscriptionStatus.ToString());
                    }

                    if (response.Trade != null)
                    {

                        var q = response.Trade.ToString();

                        Program.Trades[InstId].Enqueue(response.Trade.ToString());
                    }
                }
            }
            catch (Exception error)
            {

                if (error.Message.Contains("Detail=\"80001\""))
                {
                    Program.Errors.Enqueue("Stream limit exceeded 80001. Contact tinkoff support");
                }
                else
                {
                    Program.Errors.Enqueue(error.StackTrace + "  |  " + error.Message);
                }

            }

        }

        public async Task DataChanelDephts(string InstId)
        {
            try
            {

                var serviceCollection = new ServiceCollection();
                serviceCollection.AddInvestApiClient(TokenHandler);
                var serviceProvider = serviceCollection.BuildServiceProvider();

                var client = serviceProvider.GetRequiredService<InvestApiClient>();

                var stream = client.MarketDataStream.MarketDataStream();


                await stream.RequestStream.WriteAsync(new MarketDataRequest
                {

                    SubscribeOrderBookRequest = new SubscribeOrderBookRequest
                    {
                        Instruments =
                        {
                            new OrderBookInstrument
                            {
                                Figi = InstId,
                                Depth = 10
                            }
                        },
                        SubscriptionAction = SubscriptionAction.Subscribe
                    }
                });

                DateTime TimeSleep = DateTime.Now;

                await foreach (var response in stream.ResponseStream.ReadAllAsync())
                {

                    if (response.SubscribeOrderBookResponse != null)
                    {
                        Console.WriteLine("OrderBookChanel " +
                            response.SubscribeOrderBookResponse.OrderBookSubscriptions[0].Figi +
                            ": " +
                            response.SubscribeOrderBookResponse.OrderBookSubscriptions[0].SubscriptionStatus.ToString());
                    }

                    if (response.Orderbook != null)
                    {
                        var q = response.Orderbook.ToString();
                        if (TimeSleep.AddMilliseconds(500) < DateTime.Now)
                        {
                            Program.Depths[InstId].Enqueue(response.Orderbook.ToString());
                            TimeSleep = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                if (error.Message.Contains("Detail=\"80001\""))
                {
                    Program.Errors.Enqueue("Stream limit exceeded 80001. Contact tinkoff support");
                }
                else
                {
                    Program.Errors.Enqueue(error.StackTrace + "  |  " + error.Message);
                }
            }

        }

        public void TokenHandler(IServiceProvider serviceProvider, InvestApiSettings settings)
        {
            settings.AccessToken = Token;
        }
    }

    // объект возврата
    public class StateObject
    {

        public const int BufferSize = 8192;


        public byte[] buffer = new byte[BufferSize];


        public StringBuilder sb = new StringBuilder();


        public Socket workSocket = null;
    }

    // сокет сервер
    public class SocketServer
    {

        private IPAddress IP;

        private IPEndPoint endPoint;

        private Socket Server;

        private static ManualResetEvent allDone;

        private Func<string, string> HeadlerResponse;

        public SocketServer(Func<string, string> func)
        {

            HeadlerResponse = func;

            allDone = new ManualResetEvent(false);

            IP = IPAddress.Parse("127.0.0.1");
            endPoint = new IPEndPoint(IP, 7980);

            Server = new Socket(IP.AddressFamily,
            SocketType.Stream, ProtocolType.Tcp);

            Server.Bind(endPoint);

            Server.Listen(1000);

            Console.WriteLine("Start Server: localhost " + endPoint.Port);

        }

        public void StartServer()
        {

            while (true)
            {
                try
                {
                    allDone.Reset();

                    Server.BeginAccept(new AsyncCallback(AsyncResponse), Server);

                    allDone.WaitOne();
                }
                catch (Exception error)
                {
                    Program.Errors.Enqueue(error.StackTrace + "  |  " + error.Message);
                }

            }

        }

        private void AsyncResponse(IAsyncResult ar)
        {
            allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, SocketFlags.None,
                new AsyncCallback(ReadResponse), state);
        }

        private void ReadResponse(IAsyncResult ar)
        {
            string content = String.Empty;

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                state.sb.Append(Encoding.UTF8.GetString(
                    state.buffer, 0, bytesRead));

                content = state.sb.ToString();

                if (content.Equals("Both"))
                {
                    CloseConnection(handler);
                }
                else
                {
                    WorkingPlace(handler, content);
                }
            }
        }

        private void WorkingPlace(Socket handler, string content)
        {

            while (true)
            {
                try
                {
                    var str = HeadlerResponse(content);

                    if (str == null || str.Equals(String.Empty))
                    {
                        continue;
                    }

                    Send(handler, str);

                    if (ClientIsCloseStream(handler))
                    {
                        break;
                    }

                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private bool ClientIsCloseStream(Socket handler)
        {
            var recivedData = new byte[1024];

            handler.Receive(recivedData);

            var responceString = Encoding.UTF8.GetString(recivedData);

            if (responceString.Equals("Both"))
            {
                CloseConnection(handler);
                return true;
            }

            return false;
        }

        private void Send(Socket handler, String data)
        {
            byte[] byteData = Encoding.UTF8.GetBytes(data);

            handler.Send(byteData);
        }

        private void CloseConnection(Socket handler)
        {
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
    }
}
