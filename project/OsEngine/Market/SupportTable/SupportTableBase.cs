using System.Collections.Generic;

namespace OsEngine.Market.SupportTable
{
    public class SupportTableBase
    {
        public static List<SupportConnection> GetMoexSupportList()
        {
            List<SupportConnection> supportList = new List<SupportConnection>();

            SupportConnection alor = new SupportConnection()
            {
                ServerType = ServerType.Alor,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\alor.png",
                LingSiteUrl = "https://www.alorbroker.ru/open?pr=L0745",
                Discount = 0
            };
            supportList.Add(alor);

            SupportConnection quikDDE = new SupportConnection()
            {
                ServerType = ServerType.QuikDde,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\QuikDde.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(quikDDE);

            SupportConnection transaq = new SupportConnection()
            {
                ServerType = ServerType.Transaq,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\transaq.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(transaq);

            SupportConnection tinkoff = new SupportConnection()
            {
                ServerType = ServerType.TinkoffInvestments,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Moex\\Tinkoff.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(tinkoff);

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

            SupportConnection plaza = new SupportConnection()
            {
                ServerType = ServerType.Plaza,
                SupportType = SupportServerType.No,
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

            SupportConnection ninja = new SupportConnection()
            {
                ServerType = ServerType.NinjaTrader,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\International\\Ninja.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(ninja);

            SupportConnection lmax = new SupportConnection()
            {
                ServerType = ServerType.Lmax,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\International\\Lmax.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(lmax);

            return supportList;
        }

        public static List<SupportConnection> GetCryptoSupportList()
        {
            List<SupportConnection> supportList = new List<SupportConnection>();

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

            SupportConnection bybit = new SupportConnection()
            {
                ServerType = ServerType.Bybit,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Bybit.png",
                LingSiteUrl = "https://partner.bybit.com/b/osengine",
                Discount = 20
            };
            supportList.Add(bybit);

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
                ServerType = ServerType.AscendEx_BitMax,
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

            SupportConnection bitMart = new SupportConnection()
            {
                ServerType = ServerType.BitMart,
                SupportType = SupportServerType.Prime,
                LinqToLogo = "\\Images\\Connections\\Crypto\\BitMart.png",
                LingSiteUrl = "https://www.bitmart.com/invite/cNtynY/en",
                Discount = 0
            };
            supportList.Add(bitMart);

            SupportConnection bitmex = new SupportConnection()
            {
                ServerType = ServerType.BitMex,
                SupportType = SupportServerType.Standart,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Bitmex.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(bitmex);

            SupportConnection bitstamp = new SupportConnection()
            {
                ServerType = ServerType.BitStamp,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Bitstamp.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(bitstamp);

            SupportConnection exMo = new SupportConnection()
            {
                ServerType = ServerType.Exmo,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Exmo.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(exMo);

            SupportConnection hitBtc = new SupportConnection()
            {
                ServerType = ServerType.Hitbtc,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Crypto\\HitBtc.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(hitBtc);

            SupportConnection kraken = new SupportConnection()
            {
                ServerType = ServerType.Kraken,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Kraken.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(kraken);

            SupportConnection zb = new SupportConnection()
            {
                ServerType = ServerType.Kraken,
                SupportType = SupportServerType.No,
                LinqToLogo = "\\Images\\Connections\\Crypto\\Zb.png",
                LingSiteUrl = "",
                Discount = 0
            };
            supportList.Add(zb);

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