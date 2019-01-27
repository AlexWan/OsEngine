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
            "Eng:Show extra log window_"+
            "Ru:Показывать окно экстренного лога_");

        public string ExtraLogSoundLabel => OsLocalization.ConvertToLocString(
            "Eng:Extra log sound_"+
            "Ru:Звук экстренного лога_");

        public string TransactionSoundLabel => OsLocalization.ConvertToLocString(
            "Eng:Transaction sound_"+
            "Ru:Звук по сделке_");

        public string TextBoxMessageToUsers => OsLocalization.ConvertToLocString(
            "Eng: We apologize for any inconvenience with the translation. \nSupport our Open Source project and it will become better. \nOur bitcoin wallet \n13QyxgsGrMtTB3SggPx7hqjW3yHi68Qeyz"
            +
            "_Ru:Помогите нам стать лучше! Напишите на наш форум если нашли ошибку. А лучше переведите разработчикам немного биткойнов.\nКошелёк \n13QyxgsGrMtTB3SggPx7hqjW3yHi68Qeyz_");
    }
}
