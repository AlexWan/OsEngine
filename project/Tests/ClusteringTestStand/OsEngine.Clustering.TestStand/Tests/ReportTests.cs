/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.IO;
using System.Text.Json;
using OsEngine.Clustering.TestStand;

namespace OsEngine.Clustering.TestStand.Tests
{
    /// <summary>
    /// Модуль 5. Отчёт: валидация сохранённого файла аллокации.
    /// </summary>
    public class ReportTests : ClusteringTestsBase
    {
        private readonly string _savePath;

        public ReportTests(TestContext context) : base(context, "Report")
        {
            _savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "allocation_report_test.txt");
        }

        public void RunAll()
        {
            if (!EnsureSetup())
            {
                RecordFail("setup", "режим не открыт или боты не загружены");
                return;
            }

            if (!SaveResultFile())
            {
                RecordFail("setup", "не удалось сохранить файл аллокации для проверки");
                return;
            }

            TestHeader();
            TestBotLines();
        }

        private bool SaveResultFile()
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
            }

            QuietCall("allocation_clustering_calculate", new { });

            string response = QuietCall("allocation_result_save", new { file_path = _savePath });

            if (!TryParseJson(response, out JsonElement root)
                || !root.TryGetProperty("saved", out JsonElement saved)
                || saved.ValueKind != JsonValueKind.True)
            {
                return false;
            }

            return File.Exists(_savePath);
        }

        private void TestHeader()
        {
            const string method = "report.header";

            string[] lines = File.ReadAllLines(_savePath);

            if (lines.Length == 0)
            {
                RecordFail(method, "файл пустой");
                return;
            }

            if (!lines[0].Contains("Num cluster"))
            {
                RecordFail(method, "в заголовке нет «Num cluster»");
                return;
            }

            if (!lines[0].Contains("100%") || !lines[0].Contains("600%"))
            {
                RecordFail(method, "в заголовке нет колонок весов 100%..600%");
                return;
            }

            RecordPass(method, "заголовок валиден");
        }

        private void TestBotLines()
        {
            const string method = "report.bots";

            string[] lines = File.ReadAllLines(_savePath);

            int botLines = 0;
            int clusterLines = 0;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(';');

                // строка кластера: номер + 10 пустых
                if (parts.Length >= 11 && parts[1] == string.Empty && parts[2] == string.Empty && parts[0].Trim() != string.Empty)
                {
                    clusterLines++;
                }
                // строка бота: пустой номер + № + имя
                else if (parts.Length >= 3 && parts[0] == string.Empty && parts[1] != string.Empty && parts[2] != string.Empty)
                {
                    botLines++;
                }
            }

            if (clusterLines == 0)
            {
                RecordFail(method, "нет ни одной строки кластера");
                return;
            }

            if (botLines == 0)
            {
                RecordFail(method, "нет ни одной строки бота");
                return;
            }

            RecordPass(method, $"кластеров={clusterLines}, строк ботов={botLines}");
        }
    }
}
