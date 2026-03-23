using OsEngine.Entity;
using OsEngine.Entity.SynteticBondEntity;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using static Google.Api.LabelDescriptor.Types;

namespace OsEngine.OsTrader.Isberg
{
    public class ArbitrationParameters
    {
        /// <summary>
        /// The position that the iceberg module is currently managing | Позиция, которую ведёт айсберг-модуль
        /// </summary>
        public Position CurrentPosition;

        /// <summary>
        /// Current arbitration mode | Текущий режим арбитража
        /// </summary>
        public ArbitrationMode CurrentArbitrationMode;

        /// <summary>
        /// Current status of arbitration | Текущий статус арбитража
        /// </summary>
        public ArbitrationStatus CurrentArbitrationStatus;

        /// <summary>
        /// Iceberg object | Объект айсберга
        /// </summary>
        public BotTabSimple BotTab;

        /// <summary>
        /// The asset based on which the trading volume for the positions will be calculated | Актив, исходя из которого будет считаться торговый объем для позиций
        /// </summary>
        public string AssetPortfolio = "Prime";

        /// <summary>
        /// The direction of positions | Направление позиций
        /// </summary>
        public Side Side;

        /// <summary>
        /// Volume type for orders | Тип объема для ордеров
        /// </summary>
        public VolumeType VolumeType;

        /// <summary>
        /// The total volume. The volume that needs to be typed for security | Объём общий. Объём который нужно набрать для бумаги 
        /// </summary>
        public decimal TotalVolume;

        /// <summary>
        /// Volume for 1 entry order | Объем для 1 ордера на вход
        /// </summary>
        public decimal EnterOneOrderVolume;

        /// <summary>
        /// Volume for 1 exit order | Объем для 1 ордера на выход
        /// </summary>
        public decimal ExitOneOrderVolume;

        /// <summary>
        /// Entry order type: Limit, Market | Тип открывающего ордера: Лимит, Маркет
        /// </summary>
        public OrderPriceType EnterOrderType;

        /// <summary>
        /// Exit order type: Limit, Market | Тип закрывающего ордера: Лимит, Маркет
        /// </summary>
        public OrderPriceType ExitOrderType;

        /// <summary>
        /// The place where the limit order will be placed for entry | Место куда будет выставляться лимитный ордер для входа
        /// </summary>
        public SynteticBondOrderPosition EnterOrderPosition;

        /// <summary>
        /// The place where the exit limit order will be placed | Место куда будет выставляться лимитный ордер для выхода
        /// </summary>
        public SynteticBondOrderPosition ExitOrderPosition;

        /// <summary>
        /// Slipping into the entrance | Проскальзывание на вход
        /// </summary>
        public decimal EnterSlippage;

        /// <summary>
        /// Slipping to the exit | Проскальзывание на выход
        /// </summary>
        public decimal ExitSlippage;

        /// <summary>
        /// The lifetime of the order when opened | Время жизни ордера при открытии
        /// </summary>
        public int EnterLifetimeOrder;

        /// <summary>
        /// The lifetime of the order at closing | Время жизни ордера при закрытии
        /// </summary>
        public int ExitLifetimeOrder;

        /// <summary>
        /// Frequency of placing entry orders, in seconds.
        /// | Частота выставления входных ордеров, в секундах.
        /// </summary>
        public int EnterOrderFrequency;

        /// <summary>
        /// Frequency of placing exit orders, in seconds.
        /// | Частота выставления выходных ордеров, в секундах.
        /// </summary>
        public int ExitOrderFrequency;

        /// <summary>
        /// Time of the last entry order placement attempt
        /// | Время последней попытки выставить входной ордер
        /// </summary>
        public DateTime LastEnterOrderTime;

        /// <summary>
        /// Time of the last exit order placement attempt
        /// | Время последней попытки выставить выходной ордер
        /// </summary>
        public DateTime LastExitOrderTime;

        /// <summary>
        /// The steps when setting positions by iceberg for entry | Шаги, при наборе позиций айсбергом для входа
        /// </summary>
        public List<ArbitrationStep> EnterArbitrationSteps;

        /// <summary>
        /// The steps when the iceberg sets positions for the exit | Шаги, при наборе позиций айсбергом для выхода
        /// </summary>
        public List<ArbitrationStep> ExitArbitrationSteps;
    }

    public class ArbitrationStep
    {
        public int NumberStep;

        public OrderStateType Status;

        public decimal VolumeStep;

        public decimal OpenVolume;

        public Order LastOrder;

        public DateTime LastTime;
    }

    public enum ArbitrationMode
    {
        OpenBuyFirstSellSecond,

        OpenSellFirstBuySecond,

        CloseScript,

        CloseAllScripts
    }

    public enum ArbitrationStatus
    {
        On,

        Pause
    }

    public enum VolumeType
    {
        Contracts,
        ContractCurrency,
        DepositPercent
    }
}
