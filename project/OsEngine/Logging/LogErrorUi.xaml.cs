﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows.Forms;
using OsEngine.Language;

namespace OsEngine.Logging
{
    /// <summary>
    /// Interaction logic for LogErrorUi.xaml
    /// Логика взаимодействия для LogErrorUi.xaml
    /// </summary>
    public partial class LogErrorUi
    {
        public LogErrorUi(DataGridView gridErrorLog)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            HostLog.Child = gridErrorLog;
            Title = OsLocalization.Logging.TitleExtraLog;
            Title = Title + " " + OsEngine.PrimeSettings.PrimeSettingsMaster.LabelInHeaderBotStation;
            
            this.Activate();
            this.Focus();
        }
    }
}
