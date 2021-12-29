using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Market.Servers;

namespace OsEngine.Journal.Assemblage
{
    public class AssemblageBotsMaster
    {
        public List<BotPanelJournal> _botsJournals;

        public AssemblageBotsMaster(List<BotPanelJournal> botsJournals)
        {
            _botsJournals = botsJournals;
            ConvertPositions();
        }

        public void Clear()
        {
            _botsJournals = null;
        }

        AssemblageBotsUi _ui;

        public void Show()
        {
            if(_ui == null)
            {
                _ui = new AssemblageBotsUi(this);

                _ui.Closed += _ui_Closed;
                _ui.Show();

            }
            else
            {
                _ui.Activate();
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        public List<Position> AllPositions
        {
            get
            {
                List<Position> poses = new List<Position>();

                for(int i = 0;i < _botsJournals.Count;i++)
                {
                    poses.AddRange(_botsJournals[i].AllPositions);
                }

                return poses;
            }
        }

        public List<PositionToAssemblage> PosesToAssemblage = new List<PositionToAssemblage>();

        public void ConvertPositions()
        {
            PosesToAssemblage.Clear();

            List<Position> poses = AllPositions;

            for(int i = 0;i < poses.Count;i++)
            {
                PositionToAssemblage pos = new PositionToAssemblage(poses[i]);
                PosesToAssemblage.Add(pos);
            }
        }


    }

    public class PositionToAssemblage
    {
        public PositionToAssemblage(Position pos)
        {
            this.Direction = pos.Direction;
            this.State = pos.State;
            this.Number = pos.Number;
            this.SecurityName = pos.SecurityName;
            this.NameBot = pos.NameBot;
            this.ProfitOperationPersent = pos.ProfitOperationPersent;
            this.ProfitOperationPunkt = pos.ProfitOperationPunkt;
            this.EntryPrice = pos.EntryPrice;
            this.ClosePrice = pos.ClosePrice;
            this.TimeCreate = pos.TimeCreate;
            this.TimeClose = pos.TimeClose;
            this.PriceStep = pos.PriceStep;
            this.PriceStepCost = pos.PriceStepCost;

        }

        /// <summary>
        /// buy / sell direction
        /// направление сделки Buy / Sell
        /// </summary>
        public Side Direction;

        /// <summary>
        /// transaction status Open / Close / Opening
        /// статус сделки Open / Close / Opening
        /// </summary>
        public PositionStateType State;

        /// <summary>
        /// position number
        /// номер позиции
        /// </summary>
        public int Number;

        /// <summary>
        /// Tool code for which the position is open
        /// Код инструмента по которому открыта позиция
        /// </summary>
        public string SecurityName;

        /// <summary>
        /// name of the bot who owns the deal
        /// имя бота, которому принадлежит сделка
        /// </summary>
        public string NameBot;

        /// <summary>
        /// the amount of profit on the operation in percent
        /// количество прибыли по операции в процентах 
        /// </summary>
        public decimal ProfitOperationPersent;

        /// <summary>
        /// the amount of profit on the operation in absolute terms
        /// количество прибыли по операции в абсолютном выражении
        /// </summary>
        public decimal ProfitOperationPunkt;

        /// <summary>
        /// position opening price
        /// цена открытия позиции
        /// </summary>
        public decimal EntryPrice;

        /// <summary>
        /// position closing price
        /// цена закрытия позиции
        /// </summary>
        public decimal ClosePrice;

        /// <summary>
        /// position creation time
        /// время создания позиции
        /// </summary>
        public DateTime TimeCreate;

        /// <summary>
        /// position closing time
        /// время закрытия позиции
        /// </summary>
        public DateTime TimeClose;

        /// <summary>
        ///
        /// position opening time. The time when the first transaction on our position passed on the exchange
        /// if the transaction is not open yet, it will return the time to create the position
        /// время открытия позиции. Время когда на бирже прошла первая сделка по нашей позиции
        /// если сделка ещё не открыта, вернёт время создания позиции
        /// </summary>
        public DateTime TimeOpen;

        /// <summary>
        /// price step cost
        /// стоимость шага цены
        /// </summary>
        public decimal PriceStepCost;

        /// <summary>
        /// price step
        /// шаг цены
        /// </summary>
        public decimal PriceStep;

    }
}
