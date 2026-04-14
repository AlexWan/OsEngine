/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Controls;
using System;
using OsEngine.Market;
using OsEngine.Entity;

namespace OsEngine.OsOptimizer
{
    /// <summary>
    /// Interaction logic for OptimizerProfitStagesSaveUi.xaml
    /// </summary>
    public partial class OptimizerProfitStagesSaveUi : Window
    {
        public OptimizerProfitStagesSaveUi(OptimizerFazeReport fazeReport)
        {
            InitializeComponent();

            _fazeReport = fazeReport;

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            TextBoxProfitDepositPercent.TextChanged += TextBoxProfitDepositPercent_TextChanged;
            TextBoxProfitPositionsPercent.TextChanged += TextBoxProfitPositionsPercent_TextChanged;
            TextBoxAverageProfitPercent.TextChanged += TextBoxAverageProfitPercent_TextChanged;
            TextBoxDrawDownPercent.TextChanged += TextBoxDrawDownPercent_TextChanged;

            PaintResultTextBox();

            this.Closed += OptimizerProfitStagesSaveUi_Closed;
        }


        private void OptimizerProfitStagesSaveUi_Closed(object sender, System.EventArgs e)
        {
            _fazeReport = null;
        }

        private void ButtonSaveResultsInFile_Click(object sender, RoutedEventArgs e)
        {




        }

        #region Set settings

        private void TextBoxDrawDownPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                DrawDownPercent = TextBoxDrawDownPercent.Text.ToDecimal();
                PaintResultTextBox();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxAverageProfitPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                AverageProfitPercent = TextBoxAverageProfitPercent.Text.ToDecimal();
                PaintResultTextBox();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxProfitPositionsPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ProfitPositionsPercent = TextBoxProfitPositionsPercent.Text.ToDecimal();
                PaintResultTextBox();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxProfitDepositPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ProfitDepositPercent = TextBoxProfitDepositPercent.Text.ToDecimal();
                PaintResultTextBox();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Sort robots

        private OptimizerFazeReport _fazeReport;

        public decimal ProfitDepositPercent = 10; 

        public decimal ProfitPositionsPercent = 10;

        public decimal AverageProfitPercent = 10;

        public decimal DrawDownPercent = 10;

        private void PaintResultTextBox()
        {
            string result = "_";

            for(int i = 0;i < _fazeReport.Reports.Count;i++)
            {
                OptimizerReport report = _fazeReport.Reports[i];

                if(report.TotalProfitPercent < ProfitDepositPercent)
                {
                    continue;
                }

                if(report.ProfitPositionPercent < ProfitPositionsPercent)
                {
                    continue;
                }

                if(report.AverageProfitPercentOneContract < AverageProfitPercent)
                {
                    continue;
                }

                if(report.MaxDrawDawn > DrawDownPercent)
                {
                    continue;
                }

                result += report.BotNum.ToString() + "_";

            }

            TextBoxResultRobots.Text = result;

        }

        #endregion
    }
}
