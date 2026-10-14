/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;
using OsEngine.OsOptimizer;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// SQLite-хранилище результатов оптимизации.
    /// Таблица runs — оглавление прогонов, таблица reports — строки таблицы результатов.
    /// Метрики хранятся типизированными полями (сортировка/поиск), а исходный отчёт —
    /// целиком в поле report_save (точное восстановление в UI).
    /// </summary>
    public class OptimizerReportDb
    {
        private readonly string _dbPath;

        private static bool _batteriesInitialized;

        public OptimizerReportDb()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine", "OptimizerResults.db");
            InitBatteries();
            EnsureSchema();
        }

        #region Save

        /// <summary>
        /// Сохранить прогон (все фазы и отчёты) в БД. Возвращает id нового прогона.
        /// </summary>
        public long Save(List<OptimizerFazeReport> fazes, string robot, string dataSet, string timeframe,
            string dateFrom, string dateTo, int iterationCount, int threads, bool isPartial)
        {
            int fazesCount = fazes == null ? 0 : fazes.Count;
            int reportsCount = 0;

            for (int i = 0; i < fazesCount; i++)
            {
                reportsCount += fazes[i].Reports.Count;
            }

            string savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            long runId = 0;

            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "INSERT INTO runs (saved_at, robot, data_set, timeframe, date_from, date_to, " +
                            "iteration_count, threads, is_partial, fazes_count, reports_count) " +
                            "VALUES (@saved_at, @robot, @data_set, @timeframe, @date_from, @date_to, " +
                            "@iteration_count, @threads, @is_partial, @fazes_count, @reports_count); " +
                            "SELECT last_insert_rowid();";

                        command.Parameters.AddWithValue("@saved_at", savedAt);
                        command.Parameters.AddWithValue("@robot", robot ?? string.Empty);
                        command.Parameters.AddWithValue("@data_set", dataSet ?? string.Empty);
                        command.Parameters.AddWithValue("@timeframe", timeframe ?? string.Empty);
                        command.Parameters.AddWithValue("@date_from", dateFrom ?? string.Empty);
                        command.Parameters.AddWithValue("@date_to", dateTo ?? string.Empty);
                        command.Parameters.AddWithValue("@iteration_count", iterationCount);
                        command.Parameters.AddWithValue("@threads", threads);
                        command.Parameters.AddWithValue("@is_partial", isPartial ? 1 : 0);
                        command.Parameters.AddWithValue("@fazes_count", fazesCount);
                        command.Parameters.AddWithValue("@reports_count", reportsCount);

                        runId = Convert.ToInt64(command.ExecuteScalar());
                    }

                    for (int fazeIndex = 0; fazeIndex < fazesCount; fazeIndex++)
                    {
                        OptimizerFazeReport fazeReport = fazes[fazeIndex];
                        OptimizerFaze faze = fazeReport.Faze;

                        for (int i = 0; i < fazeReport.Reports.Count; i++)
                        {
                            OptimizerReport report = fazeReport.Reports[i];

                            using (SqliteCommand command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText =
                                    "INSERT INTO reports (run_id, faze_type, faze_index, time_start, time_end, days, " +
                                    "bot_name, positions_count, profit_position_percent, total_profit, total_profit_percent, " +
                                    "max_draw_down, average_profit, average_profit_percent_one_contract, profit_factor, " +
                                    "pay_off_ratio, recovery, sharp_ratio, average_time_in_position, report_save) " +
                                    "VALUES (@run_id, @faze_type, @faze_index, @time_start, @time_end, @days, " +
                                    "@bot_name, @positions_count, @profit_position_percent, @total_profit, @total_profit_percent, " +
                                    "@max_draw_down, @average_profit, @average_profit_percent_one_contract, @profit_factor, " +
                                    "@pay_off_ratio, @recovery, @sharp_ratio, @average_time_in_position, @report_save);";

                                command.Parameters.AddWithValue("@run_id", runId);
                                command.Parameters.AddWithValue("@faze_type", faze.TypeFaze.ToString());
                                command.Parameters.AddWithValue("@faze_index", fazeIndex);
                                command.Parameters.AddWithValue("@time_start", faze.TimeStart.ToString("O", CultureInfo.InvariantCulture));
                                command.Parameters.AddWithValue("@time_end", faze.TimeEnd.ToString("O", CultureInfo.InvariantCulture));
                                command.Parameters.AddWithValue("@days", faze.Days);
                                command.Parameters.AddWithValue("@bot_name", report.BotName ?? string.Empty);
                                command.Parameters.AddWithValue("@positions_count", report.PositionsCount);
                                command.Parameters.AddWithValue("@profit_position_percent", Convert.ToDouble(report.ProfitPositionPercent));
                                command.Parameters.AddWithValue("@total_profit", Convert.ToDouble(report.TotalProfit));
                                command.Parameters.AddWithValue("@total_profit_percent", Convert.ToDouble(report.TotalProfitPercent));
                                command.Parameters.AddWithValue("@max_draw_down", Convert.ToDouble(report.MaxDrawDawn));
                                command.Parameters.AddWithValue("@average_profit", Convert.ToDouble(report.AverageProfit));
                                command.Parameters.AddWithValue("@average_profit_percent_one_contract", Convert.ToDouble(report.AverageProfitPercentOneContract));
                                command.Parameters.AddWithValue("@profit_factor", Convert.ToDouble(report.ProfitFactor));
                                command.Parameters.AddWithValue("@pay_off_ratio", Convert.ToDouble(report.PayOffRatio));
                                command.Parameters.AddWithValue("@recovery", Convert.ToDouble(report.Recovery));
                                command.Parameters.AddWithValue("@sharp_ratio", Convert.ToDouble(report.SharpRatio));
                                command.Parameters.AddWithValue("@average_time_in_position", report.AverageTimeInPosition ?? string.Empty);
                                command.Parameters.AddWithValue("@report_save", report.GetSaveString().ToString());

                                command.ExecuteNonQuery();
                            }
                        }
                    }

                    transaction.Commit();
                }
            }

            return runId;
        }

        #endregion

        #region List

        /// <summary>
        /// Список сохранённых прогонов (оглавление), новые сверху.
        /// Фильтры необязательны: robot (точное совпадение), date_from/date_to (по saved_at).
        /// </summary>
        public List<OptimizerRunMeta> List(string robot, string dateFrom, string dateTo)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT id, saved_at, robot, data_set, timeframe, date_from, date_to, ");
            sql.Append("iteration_count, threads, is_partial, fazes_count, reports_count FROM runs");

            List<string> conditions = new List<string>();

            if (string.IsNullOrEmpty(robot) == false)
            {
                conditions.Add("robot = @robot");
            }

            if (string.IsNullOrEmpty(dateFrom) == false)
            {
                conditions.Add("saved_at >= @date_from");
            }

            if (string.IsNullOrEmpty(dateTo) == false)
            {
                conditions.Add("saved_at <= @date_to");
            }

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE " + string.Join(" AND ", conditions.ToArray()));
            }

            sql.Append(" ORDER BY id DESC");

            List<OptimizerRunMeta> result = new List<OptimizerRunMeta>();

            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql.ToString();

                    if (string.IsNullOrEmpty(robot) == false)
                    {
                        command.Parameters.AddWithValue("@robot", robot);
                    }

                    if (string.IsNullOrEmpty(dateFrom) == false)
                    {
                        command.Parameters.AddWithValue("@date_from", NormalizeDate(dateFrom, true));
                    }

                    if (string.IsNullOrEmpty(dateTo) == false)
                    {
                        command.Parameters.AddWithValue("@date_to", NormalizeDate(dateTo, false));
                    }

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OptimizerRunMeta meta = new OptimizerRunMeta();
                            meta.RunId = reader.GetInt64(0);
                            meta.SavedAt = reader.GetString(1);
                            meta.Robot = reader.GetString(2);
                            meta.DataSet = reader.GetString(3);
                            meta.Timeframe = reader.GetString(4);
                            meta.DateFrom = reader.GetString(5);
                            meta.DateTo = reader.GetString(6);
                            meta.IterationCount = Convert.ToInt32(reader.GetValue(7));
                            meta.Threads = Convert.ToInt32(reader.GetValue(8));
                            meta.IsPartial = Convert.ToInt32(reader.GetValue(9)) == 1;
                            meta.FazesCount = Convert.ToInt32(reader.GetValue(10));
                            meta.ReportsCount = Convert.ToInt32(reader.GetValue(11));
                            result.Add(meta);
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Load

        /// <summary>
        /// Загрузить прогон и собрать список фаз с отчётами.
        /// </summary>
        public List<OptimizerFazeReport> Load(long runId)
        {
            List<OptimizerFazeReport> result = new List<OptimizerFazeReport>();

            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT faze_type, faze_index, time_start, time_end, report_save " +
                        "FROM reports WHERE run_id = @run_id ORDER BY faze_index, id;";
                    command.Parameters.AddWithValue("@run_id", runId);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        OptimizerFazeReport current = null;
                        int currentFazeIndex = -1;

                        while (reader.Read())
                        {
                            int fazeIndex = Convert.ToInt32(reader.GetValue(1));

                            if (current == null || fazeIndex != currentFazeIndex)
                            {
                                currentFazeIndex = fazeIndex;
                                current = new OptimizerFazeReport();

                                OptimizerFaze faze = new OptimizerFaze();
                                OptimizerFazeType typeFaze;
                                Enum.TryParse(reader.GetString(0), out typeFaze);
                                faze.TypeFaze = typeFaze;
                                faze.TimeStart = Convert.ToDateTime(reader.GetString(2), CultureInfo.InvariantCulture);
                                faze.TimeEnd = Convert.ToDateTime(reader.GetString(3), CultureInfo.InvariantCulture);
                                current.Faze = faze;

                                result.Add(current);
                            }

                            OptimizerReport report = new OptimizerReport();
                            report.LoadFromString(reader.GetString(4));
                            current.Reports.Add(report);
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Delete

        /// <summary>
        /// Удалить прогон и все его строки.
        /// </summary>
        public void Delete(long runId)
        {
            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM reports WHERE run_id = @run_id;";
                        command.Parameters.AddWithValue("@run_id", runId);
                        command.ExecuteNonQuery();
                    }

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM runs WHERE id = @run_id;";
                        command.Parameters.AddWithValue("@run_id", runId);
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Удалить прогоны, сохранённые раньше указанной даты (включительно по старшинству).
        /// Возвращает число удалённых прогонов.
        /// </summary>
        public int Cleanup(string beforeDate)
        {
            string before = NormalizeDate(beforeDate, true);

            int deleted = 0;

            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM reports WHERE run_id IN (SELECT id FROM runs WHERE saved_at < @before);";
                        command.Parameters.AddWithValue("@before", before);
                        command.ExecuteNonQuery();
                    }

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM runs WHERE saved_at < @before;";
                        command.Parameters.AddWithValue("@before", before);
                        deleted = command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            return deleted;
        }

        #endregion

        #region Query

        /// <summary>
        /// Выборка отчётов прогона с сортировкой, фильтром по фазе и лимитом.
        /// </summary>
        public List<OptimizerReportRow> Query(long runId, string sortBy, string order, string fazeType, int limit)
        {
            string column = ResolveSortColumn(sortBy);
            string direction = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT bot_name, faze_type, faze_index, positions_count, profit_position_percent, ");
            sql.Append("total_profit, total_profit_percent, max_draw_down, average_profit, ");
            sql.Append("average_profit_percent_one_contract, profit_factor, pay_off_ratio, ");
            sql.Append("recovery, sharp_ratio, average_time_in_position ");
            sql.Append("FROM reports WHERE run_id = @run_id");

            if (string.IsNullOrEmpty(fazeType) == false)
            {
                sql.Append(" AND faze_type = @faze_type");
            }

            sql.Append(" ORDER BY " + column + " " + direction);

            if (limit > 0)
            {
                sql.Append(" LIMIT @limit");
            }

            List<OptimizerReportRow> result = new List<OptimizerReportRow>();

            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql.ToString();
                    command.Parameters.AddWithValue("@run_id", runId);

                    if (string.IsNullOrEmpty(fazeType) == false)
                    {
                        command.Parameters.AddWithValue("@faze_type", fazeType);
                    }

                    if (limit > 0)
                    {
                        command.Parameters.AddWithValue("@limit", limit);
                    }

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OptimizerReportRow row = new OptimizerReportRow();
                            row.BotName = reader.GetString(0);
                            row.FazeType = reader.GetString(1);
                            row.FazeIndex = Convert.ToInt32(reader.GetValue(2));
                            row.PositionsCount = Convert.ToInt32(reader.GetValue(3));
                            row.ProfitPositionPercent = Convert.ToDouble(reader.GetValue(4));
                            row.TotalProfit = Convert.ToDouble(reader.GetValue(5));
                            row.TotalProfitPercent = Convert.ToDouble(reader.GetValue(6));
                            row.MaxDrawDown = Convert.ToDouble(reader.GetValue(7));
                            row.AverageProfit = Convert.ToDouble(reader.GetValue(8));
                            row.AverageProfitPercentOneContract = Convert.ToDouble(reader.GetValue(9));
                            row.ProfitFactor = Convert.ToDouble(reader.GetValue(10));
                            row.PayOffRatio = Convert.ToDouble(reader.GetValue(11));
                            row.Recovery = Convert.ToDouble(reader.GetValue(12));
                            row.SharpRatio = Convert.ToDouble(reader.GetValue(13));
                            row.AverageTimeInPosition = reader.GetString(14);
                            result.Add(row);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Мета-информация одного прогона (или null, если прогона нет).
        /// </summary>
        public OptimizerRunMeta GetRunMeta(long runId)
        {
            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT saved_at, robot, data_set, timeframe, date_from, date_to, " +
                        "iteration_count, threads, is_partial, fazes_count, reports_count " +
                        "FROM runs WHERE id = @run_id;";
                    command.Parameters.AddWithValue("@run_id", runId);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            OptimizerRunMeta meta = new OptimizerRunMeta();
                            meta.RunId = runId;
                            meta.SavedAt = reader.GetString(0);
                            meta.Robot = reader.GetString(1);
                            meta.DataSet = reader.GetString(2);
                            meta.Timeframe = reader.GetString(3);
                            meta.DateFrom = reader.GetString(4);
                            meta.DateTo = reader.GetString(5);
                            meta.IterationCount = Convert.ToInt32(reader.GetValue(6));
                            meta.Threads = Convert.ToInt32(reader.GetValue(7));
                            meta.IsPartial = Convert.ToInt32(reader.GetValue(8)) == 1;
                            meta.FazesCount = Convert.ToInt32(reader.GetValue(9));
                            meta.ReportsCount = Convert.ToInt32(reader.GetValue(10));
                            return meta;
                        }
                    }
                }
            }

            return null;
        }

        private static string ResolveSortColumn(string sortBy)
        {
            if (string.IsNullOrEmpty(sortBy))
            {
                return "id";
            }

            string[] allowed = new string[]
            {
                "id", "bot_name", "faze_type", "faze_index", "positions_count",
                "profit_position_percent", "total_profit", "total_profit_percent",
                "max_draw_down", "average_profit", "average_profit_percent_one_contract",
                "profit_factor", "pay_off_ratio", "recovery", "sharp_ratio", "average_time_in_position"
            };

            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], sortBy, StringComparison.OrdinalIgnoreCase))
                {
                    return allowed[i];
                }
            }

            throw new ArgumentException($"Unknown sort column: {sortBy}");
        }

        #endregion

        #region Schema

        private void EnsureSchema()
        {
            string directory = Path.GetDirectoryName(_dbPath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            using (SqliteConnection connection = Open())
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "CREATE TABLE IF NOT EXISTS runs (" +
                        "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "saved_at TEXT NOT NULL, " +
                        "robot TEXT, " +
                        "data_set TEXT, " +
                        "timeframe TEXT, " +
                        "date_from TEXT, " +
                        "date_to TEXT, " +
                        "iteration_count INTEGER, " +
                        "threads INTEGER, " +
                        "is_partial INTEGER, " +
                        "fazes_count INTEGER, " +
                        "reports_count INTEGER);" +

                        "CREATE TABLE IF NOT EXISTS reports (" +
                        "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "run_id INTEGER NOT NULL, " +
                        "faze_type TEXT, " +
                        "faze_index INTEGER, " +
                        "time_start TEXT, " +
                        "time_end TEXT, " +
                        "days INTEGER, " +
                        "bot_name TEXT, " +
                        "positions_count INTEGER, " +
                        "profit_position_percent REAL, " +
                        "total_profit REAL, " +
                        "total_profit_percent REAL, " +
                        "max_draw_down REAL, " +
                        "average_profit REAL, " +
                        "average_profit_percent_one_contract REAL, " +
                        "profit_factor REAL, " +
                        "pay_off_ratio REAL, " +
                        "recovery REAL, " +
                        "sharp_ratio REAL, " +
                        "average_time_in_position TEXT, " +
                        "report_save TEXT);" +

                        "CREATE INDEX IF NOT EXISTS idx_reports_run_id ON reports(run_id);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_faze_type ON reports(faze_type);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_total_profit ON reports(total_profit);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_profit_factor ON reports(profit_factor);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_max_draw_down ON reports(max_draw_down);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_positions_count ON reports(positions_count);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_total_profit_percent ON reports(total_profit_percent);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_profit_position_percent ON reports(profit_position_percent);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_average_profit ON reports(average_profit);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_pay_off_ratio ON reports(pay_off_ratio);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_recovery ON reports(recovery);" +
                        "CREATE INDEX IF NOT EXISTS idx_reports_sharp_ratio ON reports(sharp_ratio);";

                    command.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Helpers

        private SqliteConnection Open()
        {
            return new SqliteConnection("Data Source=" + _dbPath + ";Pooling=false");
        }

        private static string NormalizeDate(string date, bool startOfDay)
        {
            string d = date.Length > 10 ? date.Substring(0, 10) : date;
            return startOfDay ? d + " 00:00:00" : d + " 23:59:59";
        }

        private static void InitBatteries()
        {
            if (_batteriesInitialized)
            {
                return;
            }

            LoadNativeSqlite();
            SQLitePCL.Batteries_V2.Init();
            _batteriesInitialized = true;
        }

        private static void LoadNativeSqlite()
        {
            try
            {
                string rid = GetWindowsRid();

                string nativePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "runtimes", rid, "native", "e_sqlite3.dll");

                if (File.Exists(nativePath))
                {
                    NativeLibrary.Load(nativePath);
                }
            }
            catch
            {
                // если нативная библиотека уже доступна обычным поиском — не падаем
            }
        }

        private static string GetWindowsRid()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                    return "win-x86";
                case Architecture.Arm64:
                    return "win-arm64";
                default:
                    return "win-x64";
            }
        }

        #endregion
    }

    /// <summary>
    /// Мета-информация одного сохранённого прогона (для optimizer_list_report_db).
    /// </summary>
    public class OptimizerRunMeta
    {
        public long RunId;
        public string SavedAt = string.Empty;
        public string Robot = string.Empty;
        public string DataSet = string.Empty;
        public string Timeframe = string.Empty;
        public string DateFrom = string.Empty;
        public string DateTo = string.Empty;
        public int IterationCount;
        public int Threads;
        public bool IsPartial;
        public int FazesCount;
        public int ReportsCount;
    }

    /// <summary>
    /// Строка таблицы результатов из БД (для optimizer_query_report_db).
    /// </summary>
    public class OptimizerReportRow
    {
        public string BotName = string.Empty;
        public string FazeType = string.Empty;
        public int FazeIndex;
        public int PositionsCount;
        public double ProfitPositionPercent;
        public double TotalProfit;
        public double TotalProfitPercent;
        public double MaxDrawDown;
        public double AverageProfit;
        public double AverageProfitPercentOneContract;
        public double ProfitFactor;
        public double PayOffRatio;
        public double Recovery;
        public double SharpRatio;
        public string AverageTimeInPosition = string.Empty;
    }
}
