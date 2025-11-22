using System.Collections.Generic;

namespace OsEngine.Market.SupportTable
{
    public class SupportTableBase
    {
        public static List<SupportConnection> GetMoexSupportList()
        {
            List<SupportConnection> supportList = new List<SupportConnection>();

            SupportConnection tinvest = new SupportConnection()
            {
                ServerType = ServerType.TInvest,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\TInvest.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(tinvest);

            SupportConnection alor = new SupportConnection()
            {
                ServerType = ServerType.Alor,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\alor.png",
                LingSiteUrl = "https://www.alorbroker.ru/open?pr=L0745",
                Discount = 0
            };
            supportList.Add(alor);

            // TODO Добавить Finam gRPC

            SupportConnection transaq = new SupportConnection()
            {
                ServerType = ServerType.Transaq,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\transaq.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(transaq);

            SupportConnection quikLua = new SupportConnection()
            {
                ServerType = ServerType.QuikLua,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\QLua.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(quikLua);

            SupportConnection fixFast = new SupportConnection()
            {
                ServerType = ServerType.MoexFixFastSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\FixFast.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(fixFast);

            SupportConnection fixFastCur = new SupportConnection()
            {
                ServerType = ServerType.MoexFixFastCurrency,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\FixFast.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(fixFastCur);

            SupportConnection fixFastForts = new SupportConnection()
            {
                ServerType = ServerType.MoexFixFastTwimeFutures,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\FixFast.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(fixFastForts);

            SupportConnection plaza = new SupportConnection()
            {
                ServerType = ServerType.Plaza,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\Plaza2.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(plaza);

            SupportConnection astsBridge = new SupportConnection()
            {
                ServerType = ServerType.AstsBridge,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Moex\\AstsBridge.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(astsBridge);

            return supportList;
        }

        public static List<SupportConnection> GetInternationalSupportList()
        {
            List<SupportConnection> supportList = new List<SupportConnection>();

            SupportConnection ib = new SupportConnection()
            {
                ServerType = ServerType.InteractiveBrokers,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\International\\Ib.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(ib);

            SupportConnection traderNet = new SupportConnection()
            {
                ServerType = ServerType.TraderNet,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\International\\Tradernet.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(traderNet);

            SupportConnection atp = new SupportConnection()
            {
                ServerType = ServerType.Atp,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\International\\ATP.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(atp);

            SupportConnection kite = new SupportConnection()
            {
                ServerType = ServerType.KiteConnect,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\International\\KiteConnect.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(kite);

            SupportConnection yahoo = new SupportConnection()
            {
                ServerType = ServerType.YahooFinance,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\International\\Yahoo.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(yahoo);

            SupportConnection polygon = new SupportConnection()
            {
                ServerType = ServerType.Polygon,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\International\\polygon-io.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(polygon);


            SupportConnection ninja = new SupportConnection()
            {
                ServerType = ServerType.NinjaTrader,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\International\\Ninja.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(ninja);

            return supportList;
        }

        public static List<SupportConnection> GetCryptoSupportList()
        {
            List<SupportConnection> supportList = new List<SupportConnection>();

            SupportConnection bybit = new SupportConnection()
            {
                ServerType = ServerType.Bybit,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Bybit.png",
                LingSiteUrl = "https://partner.bybit.com/b/osengine",
                Discount = 20
            };
            supportList.Add(bybit);

            SupportConnection binance = new SupportConnection()
            {
                ServerType = ServerType.Binance,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\BinanceSpot.png",
                LingSiteUrl = "https://accounts.binance.com/register?ref=K3L7BLL1",
                Discount = 20
            };

            supportList.Add(binance);

            SupportConnection binanceFutures = new SupportConnection()
            {
                ServerType = ServerType.BinanceFutures,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\BinansFutures.png",
                LingSiteUrl = "https://accounts.binance.com/register?ref=K3L7BLL1",
                Discount = 10
            };
            supportList.Add(binanceFutures);

            SupportConnection bitGet = new SupportConnection()
            {
                ServerType = ServerType.BitGetSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Bitget.png",
                LingSiteUrl = "https://partner.bitget.com/bg/txme90901684140842016",
                Discount = 20
            };
            supportList.Add(bitGet);

            SupportConnection bingx = new SupportConnection()
            {
                ServerType = ServerType.BingXSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\bingx.png",
                LingSiteUrl = "https://bingx.com/invite/OQLHEXTU",
                Discount = 30
            };
            supportList.Add(bingx);

            SupportConnection okx = new SupportConnection()
            {
                ServerType = ServerType.OKX,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Okx.png",
                LingSiteUrl = "https://www.okx.com/join/52450928",
                Discount = 15
            };
            supportList.Add(okx);

            SupportConnection kuCoinSpot = new SupportConnection()
            {
                ServerType = ServerType.KuCoinSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\KuCoin.png",
                LingSiteUrl = "https://www.kucoin.com/r/af/QBSQUGP7",
                Discount = 20
            };
            supportList.Add(kuCoinSpot);

            SupportConnection huobi = new SupportConnection()
            {
                ServerType = ServerType.HTXSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\HTX.png",
                LingSiteUrl = "https://www.htx.com/ru-ru/v/register/double-invite/web/?inviter_id=11345710&invite_code=jxbn7223",
                Discount = 30
            };
            supportList.Add(huobi);

            SupportConnection huobiF = new SupportConnection()
            {
                ServerType = ServerType.HTXFutures,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\HTX.png",
                LingSiteUrl = "https://www.htx.com/ru-ru/v/register/double-invite/web/?inviter_id=11345710&invite_code=jxbn7223",
                Discount = 30
            };
            supportList.Add(huobiF);

            SupportConnection gateIo = new SupportConnection()
            {
                ServerType = ServerType.GateIoSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\GateIo.png",
                LingSiteUrl = "https://www.gate.io/signup/BFlMU19Z?ref_type=103",
                Discount = 30
            };
            supportList.Add(gateIo);

            SupportConnection deribit = new SupportConnection()
            {
                ServerType = ServerType.Deribit,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Deribit.png",
                LingSiteUrl = "https://www.deribit.com/?reg=18571.8844",
                Discount = 10
            };
            supportList.Add(deribit);

            SupportConnection xt = new SupportConnection()
            {
                ServerType = ServerType.XTSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\XT.png",
                LingSiteUrl = "https://www.xt.com/ru/accounts/register?ref=QA3TMX",
                Discount = 30
            };
            supportList.Add(xt);

            SupportConnection askend = new SupportConnection()
            {
                ServerType = ServerType.AscendexSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Ascend.png",
                LingSiteUrl = "https://ascendex.com/register?inviteCode=BPEFZZW8Q",
                Discount = 25
            };
            supportList.Add(askend);

            SupportConnection pionex = new SupportConnection()
            {
                ServerType = ServerType.PionexSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\pionex.png",
                LingSiteUrl = "https://www.pionex.com/ru/signUp?r=0z11LpNQfus",
                Discount = 10
            };
            supportList.Add(pionex);

            SupportConnection woo = new SupportConnection()
            {
                ServerType = ServerType.Woo,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Woox.png",
                LingSiteUrl = "https://x.woo.org/register?ref=QMXPT8MR",
                Discount = 5
            };
            supportList.Add(woo);

            SupportConnection coinEx = new SupportConnection()
            {
                ServerType = ServerType.CoinExSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\CoinEx.png",
                LingSiteUrl = "https://www.coinex.com/register?rc=3hscg",
                Discount = 5
            };
            supportList.Add(coinEx);

            SupportConnection bitMart = new SupportConnection()
            {
                ServerType = ServerType.BitMartSpot,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\BitMartSpot.png",
                LingSiteUrl = "https://www.bitmart.com/invite/cNtynY/en", 
                Discount = 40
            };
            supportList.Add(bitMart);

            SupportConnection bitMartFutures = new SupportConnection()
            {
                ServerType = ServerType.BitMartFutures,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\BitMartFutures.png",
                LingSiteUrl = "https://www.bitmart.com/invite/cNtynY/en",
                Discount = 30
            };
            supportList.Add(bitMartFutures);

            SupportConnection bloFin = new SupportConnection()
            {
                ServerType = ServerType.BloFinFutures,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\BloFin.png",
                LingSiteUrl = "https://partner.blofin.com/d/IHJBujb",
                Discount = 20
            };
            supportList.Add(bloFin);

            SupportConnection exMo = new SupportConnection()
            {
                ServerType = ServerType.ExmoSpot,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Exmo.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(exMo);

            return supportList;
        }
    }

    public class SupportConnection
    {
        public ServerType ServerType;

        public SupportServerType SupportType;

        public string LinqToLogo;

        public string LingSiteUrl;

        public int Discount;
    }

    public enum SupportServerType
    {
        Prime,

        Standart,

        No
    }
}