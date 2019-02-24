/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Language
{
    public class PrimeSettingsMasterUiLocal
    {
        public string Title => OsLocalization.ConvertToLocString(
            "Eng:Configuration_"+
            "Ru:Общие настройки_");

        public string LanguageLabel => OsLocalization.ConvertToLocString(
            "Eng:Language"+
            "_Ru:Язык_");

        public string ShowExtraLogWindowLabel => OsLocalization.ConvertToLocString(
            "Eng:Show emergency log window_" +
            "Ru:Показывать окно экстренного лога_");

        public string ExtraLogSoundLabel => OsLocalization.ConvertToLocString(
            "Eng:Emergency log sound_" +
            "Ru:Звук экстренного лога_");

        public string TransactionSoundLabel => OsLocalization.ConvertToLocString(
            "Eng:Transaction sound_"+
            "Ru:Звук по сделке_");

        public string TextBoxMessageToUsers => OsLocalization.ConvertToLocString(
            "Eng: We apologize for any inconvenience with the translation. \nSupport our Open Source project and it will become better. \nOur bitcoin wallet \n13QyxgsGrMtTB3SggPx7hqjW3yHi68Qeyz"
            +
            "_Ru:Помогите нам стать лучше! Напишите на наш форум если нашли ошибку. А лучше переведите разработчикам немного биткойнов.\nКошелёк \n13QyxgsGrMtTB3SggPx7hqjW3yHi68Qeyz_");

        public string LabelServerTestingIsActive => OsLocalization.ConvertToLocString(
            "Eng:Server Testing Is Active_" +
            "Ru:Включить тестирование серверов_");

        public string LabelServerTestingToopTip => OsLocalization.ConvertToLocString(
            "Eng:This will significantly slow down the application, but it may help to find errors in the trade server. Do not turn on during daily trading_" +
            "Ru:Это существенно замедлит работу приложения но возможно поможет найти ошибки в работе сервера. Не включайте во время ежедневной торговли_");
    }
}
