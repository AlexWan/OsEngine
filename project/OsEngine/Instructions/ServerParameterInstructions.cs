/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using static OsEngine.Language.OsLocalization;
using System.Collections.Generic;

namespace OsEngine.Instructions
{
    public class ServerParameterInstructions
    {
        public string AllInstructionsInClassDescription
        {
            get
            {
                OsLocalType currentLanguage = OsLocalization.CurLocalization;

                if (currentLanguage == OsLocalType.Ru)
                {
                    return "Сервера подключения. Сборник статей";
                }
                else if (currentLanguage == OsLocalType.Eng)
                {
                    return "Connection servers. A collection of posts";
                }

                return "";
            }
        }

        public List<Instruction> GetInstructionsForServerType(ServerType serverType, bool needToHideParameters, bool canDoMultipleConnections)
        {
            List<Instruction> result = new List<Instruction>();

            if (serverType == ServerType.MoexFixFastSpot
                || serverType == ServerType.MoexFixFastCurrency)
            {
                result.Add(LinkMoexFixFast1);
                result.Add(LinkMoexFixFast2);
                result.Add(LinkMoexFixFast3);
                result.Add(LinkMoexFixFast4);
                result.Add(LinkMoexFixFast5);
                result.Add(LinkMoexFixFast6);
                result.Add(LinkMoexFixFast7);
                result.Add(LinkMoexFixFast8);
                result.Add(LinkMoexFixFast9);
                result.Add(LinkMoexFixFast10);
            }
            else if (serverType == ServerType.MoexFixFastTwimeFutures)
            {
                result.Add(LinkMoexFixFastTwime1);
                result.Add(LinkMoexFixFastTwime2);
                result.Add(LinkMoexFixFastTwime3);
                result.Add(LinkMoexFixFastTwime4);
                result.Add(LinkMoexFixFastTwime5);
                result.Add(LinkMoexFixFastTwime6);
            }
            else if (serverType == ServerType.Finam
                || serverType == ServerType.FinamGrpc)
            {
                result.Add(LinkFinam);
            }
            else if (serverType == ServerType.MoexAlgopack)
            {
                result.Add(LinkAlgopack);
            }
            else if (serverType == ServerType.MfdWeb)
            {
                result.Add(LinkMfd);
            }
            else if (serverType == ServerType.MoexDataServer)
            {
                result.Add(LinkMoexIss);
            }
            else if (serverType == ServerType.YahooFinance)
            {
                result.Add(LinkYahoo);
            }
            else if (serverType == ServerType.Polygon)
            {
                result.Add(LinkPolygon);
            }
            else if (serverType == ServerType.BinanceData)
            {
                result.Add(LinkBinanceData);
            }
            else if (serverType == ServerType.OKXData)
            {
                result.Add(LinkOkxData);
            }
            else if (serverType == ServerType.BybitData)
            {
                result.Add(LinkBybitData);
            }
            else if (serverType == ServerType.GateIoData)
            {
                result.Add(LinkGateIoData);
            }
            else if (serverType == ServerType.BitGetData)
            {
                result.Add(LinkBitGetData);
            }
            else if (serverType == ServerType.QscalpMarketDepth)
            {
                result.Add(LinkQscalpMarketDepth);
            }
            else if (serverType == ServerType.TInvest)
            {
                result.Add(LinkTInvest);
            }
            else if (serverType == ServerType.Alor)
            {
                result.Add(LinkAlor);
            }
            else if (serverType == ServerType.Transaq)
            {
                result.Add(LinkTransaq);
            }
            else if (serverType == ServerType.QuikLua)
            {
                result.Add(LinkQuik);
                result.Add(LinkMultiConnectQuik);
            }
            else if (serverType == ServerType.TraderNet)
            {
                result.Add(LinkTraderNet);
            }
            else if (serverType == ServerType.Plaza)
            {
                result.Add(LinkPlaza1);
                result.Add(LinkPlaza2);
                result.Add(LinkPlaza3);
                result.Add(LinkPlaza4);
                result.Add(LinkPlaza5);
                result.Add(LinkPlaza6);
            }
            else if (serverType == ServerType.Bybit)
            {
                result.Add(LinkBybit);
                result.Add(LinkBybit2);
                result.Add(LinkBybit3);
            }
            else if (serverType == ServerType.Binance
                || serverType == ServerType.BinanceFutures)
            {
                result.Add(LinkBinance);
                result.Add(LinkBinance2);
            }
            else if (serverType == ServerType.BitGetSpot
                || serverType == ServerType.BitGetFutures)
            {
                result.Add(LinkBitGet);
            }
            else if (serverType == ServerType.KuCoinSpot
                || serverType == ServerType.KuCoinFutures)
            {
                result.Add(LinkKuCoin);
            }
            else if (serverType == ServerType.BingXSpot
                || serverType == ServerType.BingXFutures)
            {
                result.Add(LinkBingX);
            }
            else if (serverType == ServerType.GateIoSpot
                || serverType == ServerType.GateIoFutures)
            {
                result.Add(LinkGateIo);
            }
            else if (serverType == ServerType.PionexSpot)
            {
                result.Add(LinkPionex);
            }
            else if (serverType == ServerType.OKX)
            {
                result.Add(LinkOkx);
            }
            else if (serverType == ServerType.HTXSpot
                || serverType == ServerType.HTXFutures
                || serverType == ServerType.HTXSwap)
            {
                result.Add(LinkHtx);
            }
            else if (serverType == ServerType.BitMartSpot
                || serverType == ServerType.BitMartFutures)
            {
                result.Add(LinkBitMart);
            }
            else if (serverType == ServerType.BloFinFutures)
            {
                result.Add(LinkBloFin);
            }
            else if (serverType == ServerType.InteractiveBrokers)
            {
                result.Add(LinkInteractiveBrokers);
            }
            else if (serverType == ServerType.KiteConnect)
            {
                result.Add(LinkKiteConnect);
            }
            else if (serverType == ServerType.Atp)
            {
                result.Add(LinkAtpPlatform);
            }

            if (needToHideParameters == false)
            {
                result.Add(LinkStandardSettings);
            }

            if (canDoMultipleConnections)
            {
                result.Add(LinkMultiConnect1);
                result.Add(LinkMultiConnect2);
            }

            return result;
        }

        public Instruction LinkMoexFixFast1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MOEX FixFast Spot/Currency. Обзор информации в популярных источниках по подключению к Мосбирже по протоколам FIX/FAST",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item444"
            },
        };

        public Instruction LinkMoexFixFast2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "FIX/FAST Spot: зачем нужен, что позволяет и чем отличается от других профконнекторов к MOEX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item441"
            },
        };

        public Instruction LinkMoexFixFast3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Fix Fast Spot: где брать инструкции и мануалы",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item443"
            },
        };

        public Instruction LinkMoexFixFast4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MOEX FixFast Spot/Currency. Как выписать демосчёт",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item342"
            },
        };

        public Instruction LinkMoexFixFast5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MOEX FixFast Spot/Currency. Как настроить рабочее место для запуска",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item343"
            },
        };

        public Instruction LinkMoexFixFast6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Настройки коннектора FixFast Spot",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item344"
            },
        };

        public Instruction LinkMoexFixFast7 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Обзор кода FixFast Spot",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item358"
            },
        };

        public Instruction LinkMoexFixFast8 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Настройка коннектора FixFast Currency",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item377"
            },
        };

        public Instruction LinkMoexFixFast9 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Обзор кода FixFast Currency",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item378"
            },
        };

        public Instruction LinkMoexFixFast10 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MOEX FixFast Spot/Currency. Подключение в реальные торги к фондовой секции",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub69&items%5B%5D=item426"
            },
        };

        public Instruction LinkMoexFixFastTwime1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFutures. Зачем нужен, что позволяет и чем отличается от других профконнекторов к MOEX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub71&items%5B%5D=item447"
            },
        };

        public Instruction LinkMoexFixFastTwime2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFutures. Где брать инструкции и мануалы",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub71&items%5B%5D=item448"
            },
        };

        public Instruction LinkMoexFixFastTwime3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFutures. Как выписать демосчет",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub71&items%5B%5D=item402"
            },
        };

        public Instruction LinkMoexFixFastTwime4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFutures. Настройка подключения",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub71&items%5B%5D=item403"
            },
        };

        public Instruction LinkMoexFixFastTwime5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFutures. Запуск",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub71&items%5B%5D=item404"
            },
        };

        public Instruction LinkMoexFixFastTwime6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "MoexFixFastTwimeFutures. Обзор кода в OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub71&items%5B%5D=item405"
            },
        };

        public Instruction LinkFinam = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Finam дата сервер",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item37"
            },
        };

        public Instruction LinkAlgopack = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к MOEX ALGOPACK",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item333"
            },
        };

        public Instruction LinkMfd = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к MFD дата сервер",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item411"
            },
        };

        public Instruction LinkMoexIss = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к MOEX ISS дата сервер",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item412"
            },
        };

        public Instruction LinkYahoo = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Yahoo Finance",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item413"
            },
        };

        public Instruction LinkPolygon = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Polygon.io",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item428"
            },
        };

        public Instruction LinkBinanceData = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BinanceData",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item442"
            },
        };

        public Instruction LinkOkxData = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к OKXData Server",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item802"
            },
        };

        public Instruction LinkBybitData = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BybitData Server",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item806"
            },
        };

        public Instruction LinkGateIoData = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к GateIOData Server",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item816"
            },
        };

        public Instruction LinkBitGetData = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BitGetData Server",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item818"
            },
        };

        public Instruction LinkQscalpMarketDepth = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к QscalpMarketDepth",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub67&items%5B%5D=item843"
            },
        };

        public Instruction LinkTInvest = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Т-Инвестиции",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub65&items%5B%5D=item74"
            },
        };

        public Instruction LinkAlor = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к ALOR OpenAPI",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub65&items%5B%5D=item279"
            },
        };

        public Instruction LinkTransaq = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Transaq",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub65&items%5B%5D=item70"
            },
        };

        public Instruction LinkQuik = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Quik Lua",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub65&items%5B%5D=item253"
            },
        };

        public Instruction LinkTraderNet = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к TraderNet",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub66&items%5B%5D=item410"
            },
        };

        public Instruction LinkPlaza1 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Plaza 2. Зачем нужен, что позволяет и чем отличается от других профконнекторов к MOEX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub70&items%5B%5D=item440"
            },
        };

        public Instruction LinkPlaza2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Plaza 2. Где брать инструкции и мануалы",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub70&items%5B%5D=item439"
            },
        };

        public Instruction LinkPlaza3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Plaza 2. Как выписать демосчет",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub70&items%5B%5D=item371"
            },
        };

        public Instruction LinkPlaza4 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Plaza 2. Как настроить рабочее место для запуска",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub70&items%5B%5D=item372"
            },
        };

        public Instruction LinkPlaza5 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Plaza 2. Настройки коннектора",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub70&items%5B%5D=item373"
            },
        };

        public Instruction LinkPlaza6 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Plaza 2. Обзор кода - архитектура и модули",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub70&items%5B%5D=item374"
            },
        };

        public Instruction LinkBybit = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Bybit",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item297"
            },
        };

        public Instruction LinkBybit2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Основные ошибки на Bybit: код, описание, перевод. Часть I. OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item51"
            },
        };

        public Instruction LinkBybit3 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Основные ошибки на Bybit: код, описание, перевод. Часть II. OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item52"
            },
        };

        public Instruction LinkBinance = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Binance",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item67"
            },
        };

        public Instruction LinkBinance2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Самые частые ошибки на Binance. Пути их решения. OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item35"
            },
        };

        public Instruction LinkBitGet = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BitGet",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item132"
            },
        };

        public Instruction LinkKuCoin = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к KuCoin",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item281"
            },
        };

        public Instruction LinkBingX = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BingX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item288"
            },
        };

        public Instruction LinkGateIo = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Gate IO",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item284"
            },
        };

        public Instruction LinkPionex = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Pionex",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item321"
            },
        };

        public Instruction LinkOkx = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к OKX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item77"
            },
        };

        public Instruction LinkHtx = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к HTX",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item75"
            },
        };

        public Instruction LinkBitMart = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BitMart",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item393"
            },
        };

        public Instruction LinkBloFin = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к BloFin",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub17&items%5B%5D=item436"
            },
        };

        public Instruction LinkInteractiveBrokers = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Interactive Brokers",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub66&items%5B%5D=item255"
            },
        };

        public Instruction LinkKiteConnect = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к Kite Connect",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub66&items%5B%5D=item422"
            },
        };

        public Instruction LinkAtpPlatform = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение к ATPlatform",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub66&items%5B%5D=item427"
            },
        };

        public Instruction LinkMultiConnect1 = new Instruction()
        {
            Type = InstructionType.Video,

            Ru = new InstructionLocalized()
            {
                Description = "Мультиконнект. Торговля многими счетами из одного терминала OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub75&items%5B%5D=item445"
            },
        };

        public Instruction LinkMultiConnect2 = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Прокси при Мультиконнекте в OsEngine. Торговля на десятках счетов",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub75&items%5B%5D=item446"
            },
        };

        public Instruction LinkMultiConnectQuik = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Подключение нескольких терминалов Quik к OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub75&items%5B%5D=item842"
            },
        };

        public Instruction LinkStandardSettings = new Instruction()
        {
            Type = InstructionType.Post,

            Ru = new InstructionLocalized()
            {
                Description = "Стандартные настройки коннектора OsEngine",
                PostLink = "https://o-s-a.net/os-engine-faq?cats%5B%5D=tab10&subcats%5B%5D=sub34&items%5B%5D=item380"
            },
        };
    }
}
