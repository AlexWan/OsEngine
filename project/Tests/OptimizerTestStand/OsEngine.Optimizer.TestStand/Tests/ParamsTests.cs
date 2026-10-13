/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using OsEngine.Optimizer.TestStand;

namespace OsEngine.Optimizer.TestStand.Tests
{
    /// <summary>
    /// Модуль 5 — Параметры: перебор всех числовых параметров (по одному и вместе),
    /// дробный шаг, строковые параметры, число проходов, сброс, потоки.
    /// </summary>
    public class ParamsTests : OsOptimizerTestsBase
    {
        public ParamsTests(TestContext context) : base(context, "Params")
        {
        }

        public void RunAll()
        {
            if (!Setup())
            {
                return;
            }

            EnumerationAll();
            MultiEnumeration();
            DecimalStep();
            FixedValue();
            StepTypeGet();
            StepTypeAffects();
            StringParams();
            Reset();
            Threads();
            ClearAllEnumeration();
        }

        private bool Setup()
        {
            if (!SelectDataSet() || !WaitForStorageLoad(60))
            {
                RecordFail("Setup", "не удалось выбрать сет / загрузить хранилище");
                return false;
            }

            if (!CreateBot("SmaTrendSample"))
            {
                RecordFail("CreateBot", "робот не загрузился");
                return false;
            }

            string tabName = GetTabName("Simple");

            if (string.IsNullOrEmpty(tabName) || !ConfigureSimpleTab(tabName, "SBER.txt", "Min30"))
            {
                RecordFail("ConfigureTab", "вкладка Simple не настроилась");
                return false;
            }

            return true;
        }

        private void EnumerationAll()
        {
            List<ParamInfo> numeric = GetNumericParams();

            if (numeric.Count == 0)
            {
                RecordFail("EnumerationAll", "нет числовых параметров");
                return;
            }

            bool allOk = true;
            List<string> failed = new List<string>();

            foreach (ParamInfo param in numeric)
            {
                if (!SetEnumeration(param.Name, 20, 21, 1))
                {
                    allOk = false;
                    failed.Add(param.Name + "(set)");
                    continue;
                }

                if (!IsParamOn(param.Name))
                {
                    allOk = false;
                    failed.Add(param.Name + "(on=false)");
                }

                ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = param.Name, on = false } } });
            }

            if (allOk)
            {
                RecordPass("EnumerationAll", $"все {numeric.Count} числовых параметров включаются в перебор");
            }
            else
            {
                RecordFail("EnumerationAll", $"не включились: {string.Join(", ", failed)}");
            }
        }

        private void MultiEnumeration()
        {
            List<ParamInfo> numeric = GetNumericParams();

            if (numeric.Count < 2)
            {
                RecordFail("MultiEnumeration", "меньше двух числовых параметров");
                return;
            }

            string param1 = numeric[0].Name;
            string param2 = numeric[1].Name;

            List<EnumParam> list = new List<EnumParam>();
            list.Add(new EnumParam { Name = param1, Start = 20, Stop = 21, Step = 1 });
            list.Add(new EnumParam { Name = param2, Start = 20, Stop = 21, Step = 1 });

            if (!SetEnumerationMany(list))
            {
                RecordFail("MultiEnumeration", "не удалось задать перебор двух параметров");
                return;
            }

            int passCount = GetPassCount();

            if (passCount != 8)
            {
                RecordFail("MultiEnumeration", $"ожидалось 8 проходов (2×2×2 фазы), получено {passCount}");
                return;
            }

            if (!StartAndWait(600))
            {
                RecordFail("MultiEnumeration", "прогон не завершился за отведённое время");
                return;
            }

            ReportSummary summary = GetReportSummary();

            if (summary.ReportsCount == 8)
            {
                RecordPass("MultiEnumeration", $"{param1}×{param2}: 8 проходов, 8 отчётов");
            }
            else
            {
                RecordFail("MultiEnumeration", $"ожидалось 8 отчётов, получено {summary.ReportsCount}");
            }
        }

        private void DecimalStep()
        {
            List<ParamInfo> decimals = GetDecimalParams();

            if (decimals.Count == 0)
            {
                RecordFail("DecimalStep", "нет Decimal-параметров");
                return;
            }

            string paramName = decimals[0].Name;

            if (!SetEnumeration(paramName, 1m, 2m, 0.5m))
            {
                RecordFail("DecimalStep", "не удалось задать дробный шаг");
                return;
            }

            int passCount = GetPassCount();

            if (passCount == 6)
            {
                RecordPass("DecimalStep", $"{paramName}: шаг 0.5 → 3 значения × 2 фазы = {passCount} проходов");
            }
            else
            {
                RecordFail("DecimalStep", $"ожидалось 6 проходов, получено {passCount}");
            }
        }

        private void FixedValue()
        {
            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName))
            {
                RecordFail("FixedValue", "не найден числовой параметр");
                return;
            }

            int originalDefault = GetParamIntField(paramName, "default_value", -1);
            int newValue = originalDefault == 25 ? 30 : 25;

            ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = paramName, value = newValue } } });

            int valueAfter = GetParamIntField(paramName, "value", -1);
            int defaultAfter = GetParamIntField(paramName, "default_value", -1);

            ToolsCall("optimizer_params_reset", new { });

            if (valueAfter == newValue && defaultAfter == newValue)
            {
                RecordPass("FixedValue", $"значение по умолчанию '{paramName}' = {newValue} установлено и прочитано");
            }
            else
            {
                RecordFail("FixedValue", $"value={valueAfter}, default={defaultAfter}, ожидалось {newValue}");
            }
        }

        private void StepTypeGet()
        {
            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName))
            {
                RecordFail("StepTypeGet", "не найден числовой параметр");
                return;
            }

            ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = paramName, step_type = "Percent" } } });

            string get = QuietCall("optimizer_params_get", new { });

            bool ok = false;

            if (TryParseJson(get, out JsonElement root)
                && root.TryGetProperty("parameters", out JsonElement parameters)
                && parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement parameter in parameters.EnumerateArray())
                {
                    string name = parameter.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (name != paramName)
                    {
                        continue;
                    }

                    string stepType = parameter.TryGetProperty("step_type", out JsonElement stepTypeElement)
                        ? stepTypeElement.GetString() ?? string.Empty
                        : string.Empty;

                    ok = stepType == "Percent";
                    break;
                }
            }

            ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = paramName, step_type = "Absolute" } } });

            if (ok)
            {
                RecordPass("StepTypeGet", $"step_type='Percent' установлен и прочитан ('{paramName}')");
            }
            else
            {
                RecordFail("StepTypeGet", "step_type не установился");
            }
        }

        private void StepTypeAffects()
        {
            string paramName = FindIntParamName();

            if (string.IsNullOrEmpty(paramName))
            {
                RecordFail("StepTypeAffects", "не найден числовой параметр");
                return;
            }

            if (!SetEnumeration(paramName, 100, 200, 10))
            {
                RecordFail("StepTypeAffects", "не удалось задать перебор");
                return;
            }

            ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = paramName, step_type = "Percent" } } });
            int percentCount = GetPassCount();

            ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = paramName, step_type = "Absolute" } } });
            int absoluteCount = GetPassCount();

            if (percentCount > 0 && absoluteCount > 0 && percentCount != absoluteCount)
            {
                RecordPass("StepTypeAffects", $"step_type влияет на число проходов: Percent={percentCount}, Absolute={absoluteCount} ('{paramName}')");
            }
            else
            {
                RecordFail("StepTypeAffects", $"число проходов не изменилось: Percent={percentCount}, Absolute={absoluteCount} ('{paramName}')");
            }
        }

        private void StringParams()
        {
            List<ParamInfo> stringParams = GetStringParams();

            if (stringParams.Count == 0)
            {
                RecordFail("StringParams", "нет строковых параметров");
                return;
            }

            bool allOk = true;
            List<string> turnedOn = new List<string>();

            foreach (ParamInfo param in stringParams)
            {
                string response = ToolsCall("optimizer_params_set", new
                {
                    parameters = new object[] { new { name = param.Name, on = true } }
                });

                if (IsError(response))
                {
                    allOk = false;
                    continue;
                }

                if (IsParamOn(param.Name))
                {
                    turnedOn.Add(param.Name);
                }
                else
                {
                    allOk = false;
                }

                ToolsCall("optimizer_params_set", new { parameters = new object[] { new { name = param.Name, on = false } } });
            }

            if (allOk && turnedOn.Count == stringParams.Count)
            {
                RecordPass("StringParams", $"включаются ({string.Join(", ", turnedOn)}), но не перебираются");
            }
            else
            {
                RecordFail("StringParams", "не все строковые параметры включились");
            }
        }

        private void Reset()
        {
            string response = ToolsCall("optimizer_params_reset", new { });

            if (IsError(response))
            {
                RecordFail("Reset", GetErrorText(response));
                return;
            }

            RecordPass("Reset", "параметры сброшены на стандартные");
        }

        private void Threads()
        {
            string getOriginal = QuietCall("optimizer_get_threads", new { });

            int original = 8;

            if (TryParseJson(getOriginal, out JsonElement originalRoot)
                && originalRoot.TryGetProperty("threads_count", out JsonElement originalElement)
                && originalElement.ValueKind == JsonValueKind.Number)
            {
                original = originalElement.GetInt32();
            }

            ToolsCall("optimizer_set_threads", new { threads_count = 2 });

            string getNew = ToolsCall("optimizer_get_threads", new { });

            int newCount = 0;

            if (TryParseJson(getNew, out JsonElement newRoot)
                && newRoot.TryGetProperty("threads_count", out JsonElement newElement)
                && newElement.ValueKind == JsonValueKind.Number)
            {
                newCount = newElement.GetInt32();
            }

            ToolsCall("optimizer_set_threads", new { threads_count = original });

            if (newCount == 2)
            {
                RecordPass("Threads", $"потоки выставлены и прочитаны: {newCount}");
            }
            else
            {
                RecordFail("Threads", $"ожидалось 2, получено {newCount}");
            }
        }

        private List<ParamInfo> GetNumericParams()
        {
            List<ParamInfo> result = new List<ParamInfo>();

            foreach (ParamInfo param in GetParamList())
            {
                if (param.Type == "Int" || param.Type == "Decimal" || param.Type == "DecimalCheckBox")
                {
                    result.Add(param);
                }
            }

            return result;
        }

        private List<ParamInfo> GetDecimalParams()
        {
            List<ParamInfo> result = new List<ParamInfo>();

            foreach (ParamInfo param in GetParamList())
            {
                if (param.Type == "Decimal" || param.Type == "DecimalCheckBox")
                {
                    result.Add(param);
                }
            }

            return result;
        }

        private List<ParamInfo> GetStringParams()
        {
            List<ParamInfo> result = new List<ParamInfo>();

            foreach (ParamInfo param in GetParamList())
            {
                if (param.Type == "String")
                {
                    result.Add(param);
                }
            }

            return result;
        }
    }
}
