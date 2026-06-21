/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Language
{
    public class McpApiLocal
    {
        public string Title => OsLocalization.ConvertToLocString(
            "Eng:MCP API_" +
            "Ru:MCP API_");

        public string ButtonApi => OsLocalization.ConvertToLocString(
            "Eng:API_" +
            "Ru:API_");

        public string LabelEnabled => OsLocalization.ConvertToLocString(
            "Eng:Enable API_" +
            "Ru:Включить API_");

        public string LabelPort => OsLocalization.ConvertToLocString(
            "Eng:Port_" +
            "Ru:Порт_");

        public string LabelApiKey => OsLocalization.ConvertToLocString(
            "Eng:API Key_" +
            "Ru:API Key_");

        public string LabelFullLog => OsLocalization.ConvertToLocString(
            "Eng:Full log_" +
            "Ru:Полный лог_");

        public string ButtonSave => OsLocalization.ConvertToLocString(
            "Eng:Save_" +
            "Ru:Сохранить_");

        public string ButtonRestart => OsLocalization.ConvertToLocString(
            "Eng:Restart_" +
            "Ru:Перезапустить_");
    }
}
