using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.RobotPriceChannel
{
    [Bot("RobotPriceChannel")]
    class RobotPriceChannel : BotPanel
    {
        /// Робот на основе индикатора PriceChannel c трейлинг стопом
        /// Робот будет входить в позицию Long и выходить по откату от максимально
        /// высокой точки на определенном значении (на уровне Stop Loss, он будет подтягиваться за ценой)

        public RobotPriceChannel(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple); // Cоздаем обычную вкладку, через которую получу свечки и стаканы          
            _tab = TabsSimple[0];         // Сохраняем вкладку в конструкторе, чтобы доступ был быстрее
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent; // Подписываемся на событие завершения свечи

            // Cоздаем индикатор в конструкторе
            _priceChannel = new PriceChannel(name + "channel", false);
            _priceChannel = (PriceChannel)_tab.CreateCandleIndicator(_priceChannel, "Prime"); // "Prime" - индикатор на основном чарте, где свечки
            _priceChannel.Save(); // Создаем файл с настройками и сохранением индикатора

            // Инициализация параметров в конструкторе
            IsOn = CreateParameter("Is On", false);
            VolumeRegime = CreateParameter("Volume type", "Contract currency", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeDecimals = CreateParameter("Number of Digits after the decimal point in the volume", 2, 1, 50, 4, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            PriceChannelLength = CreateParameter("Channel lenght", 20, 20, 300, 10);
            StopValue = CreateParameter("Stop", 0.5m, 0.5m, 5, 0.1m);
            ticket = "USDT";
            // Если что-то меняем после создания индикатора. Настраиваем значения в индикаторе верхнюю и нижнюю линию 
            _priceChannel.LenghtDownLine = PriceChannelLength.ValueInt;
            _priceChannel.LenghtUpLine = PriceChannelLength.ValueInt;

            // Подписываемся на событие обновления параметров пользователем
            ParametrsChangeByUser += RobotPriceChannel_ParametrsChangeByUser;

        }

        private void RobotPriceChannel_ParametrsChangeByUser()
        {
            // Когда пользователь из интерфейса будет менять параметры, индикатор будет принимать новые значения, сохранять их и перезагружать
            _priceChannel.LenghtDownLine = PriceChannelLength.ValueInt;
            _priceChannel.LenghtUpLine = PriceChannelLength.ValueInt;
            _priceChannel.Save();    // Сохраняем
            _priceChannel.Reload();  // Перезагружаем
        }

        private BotTabSimple _tab;          // Сохраняем вкладку как поле в классе RobotPriceChannelBuy
        private PriceChannel _priceChannel; // Создаем индикатор PriceChannel в виде поля

        // Cоздаем параметры для оптимизации:
        public StrategyParameterBool IsOn;               // Включен или выключен робот
        public StrategyParameterString VolumeRegime;
        public StrategyParameterInt VolumeDecimals;
        public StrategyParameterDecimal VolumeOnPosition;
        public StrategyParameterDecimal Slippage;        // Проскальзывание рассчитывается в % 
        public StrategyParameterInt PriceChannelLength;  // Длинна индикатора для оптимизации          
        public StrategyParameterDecimal StopValue;       // Параметр для стопа. Вся магия тут!!!
        public string ticket;                            // Валюта, в которой хранятся денежные средства на бирже                                                

        public override string GetNameStrategyType()
        {
            return "RobotPriceChannel";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Торговая логика:
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (IsOn.ValueBool == false)
            {
                return;
            }
            // Берем индикатор priceChannel, все его значения и сравниваем с длинной свечек
            if (_priceChannel.LenghtUpLine > candles.Count || _priceChannel.LenghtDownLine > candles.Count)
            {
                return;   // Если верняя или нижняя длинна индикатора больше количества свечек - выходим
            }

            List<Position> positions = _tab.PositionsOpenAll;

        // Для торговли реальными деньгами на биржах криптовалют decimal portfo РАЗКОММЕНТИРОВАТЬ!!!
        //decimal portfo = _tab.Portfolio.GetPositionOnBoard().Find(pos => pos.SecurityNameCode == ticket).ValueCurrent;

        decimal lastPrice = candles[candles.Count - 1].Close; // Сохраняем в переменной lastPrice цену закрытия последней свечи

            // Логика входа в позицию Long
            if (positions.Count == 0)
            {
                decimal channel = _priceChannel.ValuesUp[_priceChannel.ValuesUp.Count - 2]; // Берем предпоследнее значение верхней линии канала

                if (lastPrice > channel)
                {
                    // Для входа в позицию используем лучшую цену продажи + проскальзывание
                    _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _tab.Securiti.PriceStep * Slippage.ValueDecimal);
                }
            }

            // Логика выхода из позиции Long
            else if (positions.Count != 0)
            {
                decimal stopActivation = lastPrice - lastPrice * (StopValue.ValueDecimal / 100); // Беру последнюю цену и отнимаю от нее параметр, который ввел в оптим.
                decimal stopOrderPrice = stopActivation - _tab.Securiti.PriceStep * Slippage.ValueDecimal;
                _tab.CloseAtTrailingStop(positions[0], stopActivation, stopOrderPrice);
            }
        }

        private decimal GetVolume()
        {
            decimal volume = VolumeOnPosition.ValueDecimal;


            if (VolumeRegime.ValueString == "Contract currency") // "Валюта контракта"
            {
                decimal contractPrice = TabsSimple[0].PriceBestAsk;
                volume = Math.Round(VolumeOnPosition.ValueDecimal / contractPrice, VolumeDecimals.ValueInt);
                return volume;
            }
            else // Количество контрактов
                return volume;
        }
    }
}