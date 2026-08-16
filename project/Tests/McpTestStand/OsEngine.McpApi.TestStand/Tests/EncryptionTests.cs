/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for encryption tools (global server passwords encryption).
    /// Encrypted branch: unlock only, files are not mutated.
    /// Plain branch: enable and disable with a test password, state is restored via API.
    /// </summary>
    public class EncryptionTests
    {
        private const string Module = "ENCRYPTION";

        private const string TestPassword = "11111111";

        private const string WrongPassword = "wrongpass1";

        private readonly TestContext _context;

        public EncryptionTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            RunScenarioApiCycle();
            RunScenarioLockedMode();
        }

        /// <summary>
        /// scenario 1: full cycle via API in one process (enable, status, disable)
        /// сценарий 1: полный цикл через API в одном процессе
        /// </summary>
        private void RunScenarioApiCycle()
        {
            string status = ReadStatusChecked("encryption_get_status_initial", out bool unlocked);

            if (status == null)
            {
                return;
            }

            if (status == "Encrypted")
            {
                RunEncryptedBranch();
            }
            else
            {
                RunPlainBranch();
            }
        }

        /// <summary>
        /// scenario 2: locked mode after restart. Enable, restart, bootstrap unlock, disable
        /// сценарий 2: locked-режим после перезапуска. Включение, перезапуск, bootstrap-разблокировка, выключение
        /// </summary>
        private void RunScenarioLockedMode()
        {
            // 1. перезапуск в главное меню. Шифрование должно быть выключено

            _context.RestartOsEngine(string.Empty);

            string status = ReadStatusChecked("locked_get_status_initial", out _);

            if (status == null)
            {
                return;
            }

            if (status == "Encrypted")
            {
                _context.RecordFail(Module, "locked_scenario_start", "encryption must be off at scenario start, but it is Encrypted");
                return;
            }

            // 2. включаем шифрование и ждём перешифровку файлов

            CallToolExpectSuccess("encryption_enable", new { password = TestPassword });

            Thread.Sleep(3000);

            // 3. перезапуск главного меню - ключ API зашифрован, хост в locked-режиме

            _context.RestartOsEngine(string.Empty);

            // 4. неверный пароль отклонён, обычный метод заблокирован, верный пароль разблокирует, выключаем

            CallUnlockExpectError("locked_unlock_wrong_password", WrongPassword);

            CallExpectLockedRejection("ping");

            CallToolExpectSuccess("encryption_unlock", new { password = TestPassword });

            CheckStatus("locked_get_status_after_unlock", expectEncrypted: true, expectUnlocked: true);

            CallToolExpectSuccess("encryption_disable", new { password = TestPassword });

            // 5. ждём расшифровку файлов и проверяем, что шифрование выключено

            Thread.Sleep(3000);

            CheckStatus("locked_get_status_after_disable", expectEncrypted: false, expectUnlocked: false);
        }

        #region Branches

        private void RunEncryptedBranch()
        {
            CallToolExpectError("encryption_unlock", new { password = WrongPassword }, "wrong password rejected");

            CallToolExpectError("encryption_enable", new { password = TestPassword }, "enable rejected when already enabled");

            CallToolExpectSuccess("encryption_unlock", new { password = TestPassword });

            CheckStatus("encryption_get_status_after_unlock", expectEncrypted: true, expectUnlocked: true);
        }

        private void RunPlainBranch()
        {
            CallToolExpectError("encryption_enable", new { password = "123" }, "short password rejected");

            CallToolExpectSuccess("encryption_enable", new { password = TestPassword });

            CheckStatus("encryption_get_status_after_enable", expectEncrypted: true, expectUnlocked: true);

            CallToolExpectError("encryption_enable", new { password = TestPassword }, "second enable rejected");

            CallToolExpectError("encryption_disable", new { password = WrongPassword }, "wrong password rejected");

            CallToolExpectSuccess("encryption_disable", new { password = TestPassword });

            CheckStatus("encryption_get_status_after_disable", expectEncrypted: false, expectUnlocked: false);
        }

        #endregion

        #region Checks

        private string ReadStatusChecked(string checkName, out bool unlocked)
        {
            unlocked = false;

            const string method = "encryption_get_status";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, checkName, "IsError is true");
                        return null;
                    }

                    string text = GetContentText(result);

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        if (!root.TryGetProperty("Status", out JsonElement statusElement))
                        {
                            _context.RecordFail(Module, checkName, "Status missing");
                            return null;
                        }

                        string status = statusElement.GetString();

                        if (status != "Plain" && status != "Encrypted" && status != "Declined")
                        {
                            _context.RecordFail(Module, checkName, "unexpected Status value: " + status);
                            return null;
                        }

                        if (!root.TryGetProperty("Unlocked", out JsonElement unlockedElement))
                        {
                            _context.RecordFail(Module, checkName, "Unlocked missing");
                            return null;
                        }

                        unlocked = unlockedElement.GetBoolean();

                        _context.RecordPass(Module, checkName, "status: " + status + ", unlocked: " + unlocked);

                        return status;
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, checkName, error.Message);
                return null;
            }
        }

        private void CheckStatus(string checkName, bool expectEncrypted, bool expectUnlocked)
        {
            const string method = "encryption_get_status";
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, checkName, "IsError is true");
                        return;
                    }

                    string text = GetContentText(result);

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        string status = root.GetProperty("Status").GetString();
                        bool unlocked = root.GetProperty("Unlocked").GetBoolean();

                        bool isEncrypted = status == "Encrypted";

                        if (isEncrypted != expectEncrypted)
                        {
                            _context.RecordFail(Module, checkName, "Status is " + status + ", expected encrypted: " + expectEncrypted);
                            return;
                        }

                        if (unlocked != expectUnlocked)
                        {
                            _context.RecordFail(Module, checkName, "Unlocked is " + unlocked + ", expected: " + expectUnlocked);
                            return;
                        }

                        _context.RecordPass(Module, checkName, "status: " + status + ", unlocked: " + unlocked);
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, checkName, error.Message);
            }
        }

        private void CallExpectLockedRejection(string method)
        {
            string checkName = "locked_reject_" + method;
            object request = new { };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                _context.RecordFail(Module, checkName, "expected locked rejection, but call succeeded");
            }
            catch (Exception error)
            {
                _context.PrintResponse(error.Message);

                if (error.Message.Contains("Encryptor is locked"))
                {
                    _context.RecordPass(Module, checkName, "method rejected in locked mode");
                }
                else
                {
                    _context.RecordFail(Module, checkName, "unexpected error: " + error.Message);
                }
            }
        }

        private void CallUnlockExpectError(string checkName, string password)
        {
            const string method = "encryption_unlock";
            object request = new { password = password };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (result.TryGetProperty("IsError", out JsonElement isError) && isError.GetBoolean())
                    {
                        string text = GetContentText(result);

                        if (text.Contains("Wrong password"))
                        {
                            _context.RecordPass(Module, checkName, "wrong password rejected");
                        }
                        else
                        {
                            _context.RecordFail(Module, checkName, "unexpected error text: " + text);
                        }

                        return;
                    }

                    _context.RecordFail(Module, checkName, "expected error, but call succeeded");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse(error.Message);
                _context.RecordFail(Module, checkName, error.Message);
            }
        }

        private void CallToolExpectSuccess(string method, object request)
        {
            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        string errorText = GetContentText(result);

                        _context.RecordFail(Module, method, "IsError is true. " + errorText);
                        return;
                    }

                    string text = GetContentText(result);

                    if (text.Contains("\"Success\":true") == false)
                    {
                        _context.RecordFail(Module, method, "Success is not true. Response: " + text);
                        return;
                    }

                    _context.RecordPass(Module, method, "success");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void CallToolExpectError(string method, object request, string passMessage)
        {
            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (result.TryGetProperty("IsError", out JsonElement isError) && isError.GetBoolean())
                    {
                        _context.RecordPass(Module, method, passMessage);
                        return;
                    }

                    _context.RecordFail(Module, method, "expected error, but call succeeded");
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        #endregion

        #region Helpers

        private string GetContentText(JsonElement result)
        {
            if (result.TryGetProperty("Content", out JsonElement content) && content.GetArrayLength() > 0)
            {
                return content[0].GetProperty("Text").GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        #endregion
    }
}
