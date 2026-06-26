/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand.Tests
{
    /// <summary>
    /// Tests for OsData MCP API tools.
    /// </summary>
    public class DataTests
    {
        private const string Module = "DATA";
        private const string TestSetName = "McpTestSet";
        private const string SettingsTestSetName = "McpSettingsTestSet";
        private const string TestServerType = "Finam";

        private readonly TestContext _context;

        public DataTests(TestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void RunAll()
        {
            _context.PrintModuleHeader(Module);

            TestOpenMode();
            TestGetSets();
            TestCreateSet();
            TestDeleteSet();
            TestSetSettingsGetAndSet();
            TestSecuritiesGetAddRemove();
            TestSetOnOff();
            TestDownloadFlow();
        }

        private void TestOpenMode()
        {
            const string method = "terminal_open_mode";
            object request = new { mode = "data" };

            try
            {
                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                if (!response.Contains("Opening mode data"))
                {
                    _context.RecordFail(Module, method, "response does not contain 'Opening mode data'");
                    return;
                }

                Thread.Sleep(3000);

                _context.RecordPass(Module, method, "operation accepted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestGetSets()
        {
            const string method = "data_get_sets";
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
                        _context.RecordFail(Module, method, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        if (innerDocument.RootElement.ValueKind != JsonValueKind.Array)
                        {
                            _context.RecordFail(Module, method, "response is not an array");
                            return;
                        }

                        foreach (JsonElement item in innerDocument.RootElement.EnumerateArray())
                        {
                            if (!item.TryGetProperty("securities", out JsonElement securitiesElement)
                                || securitiesElement.ValueKind != JsonValueKind.Array)
                            {
                                _context.RecordFail(Module, method, "set item is missing 'securities' array");
                                return;
                            }

                            if (!item.TryGetProperty("securities_count", out JsonElement securitiesCountElement)
                                || securitiesCountElement.GetInt32() != securitiesElement.GetArrayLength())
                            {
                                _context.RecordFail(Module, method, "securities_count does not match securities array length");
                                return;
                            }
                        }

                        _context.RecordPass(Module, method, $"sets_count={innerDocument.RootElement.GetArrayLength()}");
                    }
                }
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestCreateSet()
        {
            const string method = "data_create_set";

            try
            {
                string sourceName = EnsureFinamServerInstance();

                if (string.IsNullOrEmpty(sourceName))
                {
                    _context.RecordFail(Module, method, "failed to ensure Finam server instance");
                    return;
                }

                // Clean up a previously failed run
                _context.Client.ToolsCall("data_delete_set", new { name = TestSetName });

                object request = new
                {
                    name = TestSetName,
                    source = TestServerType,
                    source_name = sourceName,
                    timeframes = new[] { "Min5", "Hour1", "Day" },
                    date_from = "2024-01-01T00:00:00",
                    date_to = "2024-12-31T00:00:00"
                };

                _context.PrintRequest(Module, method, request);
                string response = _context.Client.ToolsCall(method, request);
                _context.PrintResponse(response);

                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, method, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        if (!root.TryGetProperty("name", out JsonElement nameElement)
                            || nameElement.GetString() != "Set_" + TestSetName)
                        {
                            _context.RecordFail(Module, method, "created set name mismatch");
                            return;
                        }

                        if (!root.TryGetProperty("regime", out JsonElement regimeElement)
                            || regimeElement.GetString() != "Off")
                        {
                            _context.RecordFail(Module, method, "created set regime is not Off");
                            return;
                        }
                    }
                }

                _context.RecordPass(Module, method, "set created");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestDeleteSet()
        {
            const string method = "data_delete_set";
            object request = new { name = TestSetName };

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
                        _context.RecordFail(Module, method, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, method, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        if (!root.TryGetProperty("name", out JsonElement nameElement)
                            || nameElement.GetString() != "Set_" + TestSetName)
                        {
                            _context.RecordFail(Module, method, "deleted set name mismatch");
                            return;
                        }

                        if (!root.TryGetProperty("deleted", out JsonElement deletedElement)
                            || !deletedElement.GetBoolean())
                        {
                            _context.RecordFail(Module, method, "deleted flag is false");
                            return;
                        }
                    }
                }

                // Verify the set is gone
                string setsResponse = _context.Client.ToolsCall("data_get_sets", new { });

                using (var document = JsonDocument.Parse(setsResponse))
                {
                    string text = document.RootElement.GetProperty("Content")[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        foreach (JsonElement item in innerDocument.RootElement.EnumerateArray())
                        {
                            string name = item.GetProperty("name").GetString() ?? string.Empty;

                            if (name == "Set_" + TestSetName)
                            {
                                _context.RecordFail(Module, method, "set still exists after delete");
                                return;
                            }
                        }
                    }
                }

                _context.RecordPass(Module, method, "set deleted");
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, method, error.Message);
            }
        }

        private void TestSetSettingsGetAndSet()
        {
            const string createMethod = "data_create_set";
            const string getMethod = "data_set_settings_get";
            const string setMethod = "data_set_settings_set";
            const string deleteMethod = "data_delete_set";

            try
            {
                string sourceName = EnsureFinamServerInstance();

                if (string.IsNullOrEmpty(sourceName))
                {
                    _context.RecordFail(Module, getMethod, "failed to ensure Finam server instance");
                    return;
                }

                // Clean up a previously failed run
                _context.Client.ToolsCall(deleteMethod, new { name = SettingsTestSetName });

                // Create a dedicated set for settings tests
                object createRequest = new
                {
                    name = SettingsTestSetName,
                    source = TestServerType,
                    source_name = sourceName,
                    timeframes = new[] { "Min5", "Hour1", "Day" },
                    date_from = "2024-01-01T00:00:00",
                    date_to = "2024-12-31T00:00:00"
                };

                _context.Client.ToolsCall(createMethod, createRequest);

                // Get settings
                object getRequest = new { name = SettingsTestSetName };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                string initialDateTo;
                int initialMarketDepthDepth;

                using (var document = JsonDocument.Parse(getResponse))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, getMethod, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, getMethod, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        if (!root.TryGetProperty("name", out JsonElement nameElement)
                            || nameElement.GetString() != "Set_" + SettingsTestSetName)
                        {
                            _context.RecordFail(Module, getMethod, "set name mismatch");
                            return;
                        }

                        if (!root.TryGetProperty("date_to", out JsonElement dateToElement))
                        {
                            _context.RecordFail(Module, getMethod, "date_to missing");
                            return;
                        }

                        initialDateTo = dateToElement.GetString() ?? string.Empty;

                        if (!root.TryGetProperty("market_depth_depth", out JsonElement depthElement))
                        {
                            _context.RecordFail(Module, getMethod, "market_depth_depth missing");
                            return;
                        }

                        initialMarketDepthDepth = depthElement.GetInt32();
                    }
                }

                _context.RecordPass(Module, getMethod, "settings received");

                // Update settings
                object setRequest = new
                {
                    name = SettingsTestSetName,
                    settings = new
                    {
                        date_to = "2025-06-30T00:00:00",
                        timeframes = new[] { "Min5", "Hour1" },
                        market_depth_depth = 10
                    }
                };

                _context.PrintRequest(Module, setMethod, setRequest);
                string setResponse = _context.Client.ToolsCall(setMethod, setRequest);
                _context.PrintResponse(setResponse);

                using (var document = JsonDocument.Parse(setResponse))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        _context.RecordFail(Module, setMethod, "IsError is true");
                        return;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        _context.RecordFail(Module, setMethod, "Content is empty");
                        return;
                    }

                    string text = content[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        if (!root.TryGetProperty("updated", out JsonElement updatedElement)
                            || !updatedElement.GetBoolean())
                        {
                            _context.RecordFail(Module, setMethod, "updated flag is false");
                            return;
                        }
                    }
                }

                // Verify settings were updated
                string verifyResponse = _context.Client.ToolsCall(getMethod, getRequest);

                using (var document = JsonDocument.Parse(verifyResponse))
                {
                    string text = document.RootElement.GetProperty("Content")[0].GetProperty("Text").GetString() ?? string.Empty;

                    using (var innerDocument = JsonDocument.Parse(text))
                    {
                        JsonElement root = innerDocument.RootElement;

                        string dateTo = root.GetProperty("date_to").GetString() ?? string.Empty;

                        if (dateTo == initialDateTo)
                        {
                            _context.RecordFail(Module, setMethod, "date_to was not changed");
                            return;
                        }

                        if (dateTo != "2025-06-30T00:00:00")
                        {
                            _context.RecordFail(Module, setMethod, $"date_to has unexpected value: {dateTo}");
                            return;
                        }

                        int marketDepthDepth = root.GetProperty("market_depth_depth").GetInt32();

                        if (marketDepthDepth == initialMarketDepthDepth)
                        {
                            _context.RecordFail(Module, setMethod, "market_depth_depth was not changed");
                            return;
                        }

                        if (marketDepthDepth != 10)
                        {
                            _context.RecordFail(Module, setMethod, $"market_depth_depth has unexpected value: {marketDepthDepth}");
                            return;
                        }

                        if (!root.TryGetProperty("timeframes", out JsonElement timeframesElement)
                            || timeframesElement.GetArrayLength() != 2)
                        {
                            _context.RecordFail(Module, setMethod, "timeframes were not updated");
                            return;
                        }
                    }
                }

                _context.RecordPass(Module, setMethod, "settings updated and verified");

                // Delete the dedicated set
                _context.Client.ToolsCall(deleteMethod, new { name = SettingsTestSetName });
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, getMethod, error.Message);

                // Attempt cleanup
                try
                {
                    _context.Client.ToolsCall(deleteMethod, new { name = SettingsTestSetName });
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void TestSecuritiesGetAddRemove()
        {
            const string createMethod = "data_create_set";
            const string deleteMethod = "data_delete_set";
            const string getMethod = "data_set_securities_get";
            const string addMethod = "data_set_securities_add";
            const string removeMethod = "data_set_securities_remove";
            const string setName = "McpSecuritiesTestSet";
            const string serverType = "MoexDataServer";
            const string securityName = "SBER";

            try
            {
                // Activate and connect the server so securities are available.
                _context.Client.ToolsCall("server_management_activate", new { type = serverType });
                _context.Client.ToolsCall("server_instance_connect", new { type = serverType });

                bool securitiesReady = false;
                DateTime waitStart = DateTime.Now;

                while ((DateTime.Now - waitStart).TotalSeconds < 120)
                {
                    string securitiesResponse = _context.Client.ToolsCall("server_instance_get_securities", new { type = serverType });

                    if (IsSuccessResponse(securitiesResponse, out string securitiesText)
                        && !string.IsNullOrEmpty(securitiesText))
                    {
                        using (var securitiesDocument = JsonDocument.Parse(securitiesText))
                        {
                            if (securitiesDocument.RootElement.TryGetProperty("count", out JsonElement countElement)
                                && countElement.GetInt32() > 0)
                            {
                                securitiesReady = true;
                                break;
                            }
                        }
                    }

                    Thread.Sleep(2000);
                }

                if (!securitiesReady)
                {
                    _context.RecordFail(Module, addMethod, $"server '{serverType}' did not provide securities within 120 seconds");
                    return;
                }

                // Clean up a previously failed run.
                _context.Client.ToolsCall(deleteMethod, new { name = setName });

                // Create a dedicated set for securities tests.
                object createRequest = new
                {
                    name = setName,
                    source = serverType,
                    source_name = serverType,
                    timeframes = new[] { "Min1" },
                    date_from = "2024-01-01T00:00:00",
                    date_to = "2024-12-31T00:00:00"
                };

                _context.Client.ToolsCall(createMethod, createRequest);

                // Get empty list.
                object getRequest = new { name = setName };
                _context.PrintRequest(Module, getMethod, getRequest);
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!IsSuccessResponse(getResponse, out string emptyText) ||
                    string.IsNullOrEmpty(emptyText))
                {
                    _context.RecordFail(Module, getMethod, "failed to get empty securities list");
                    return;
                }

                using (var emptyDocument = JsonDocument.Parse(emptyText))
                {
                    if (emptyDocument.RootElement.ValueKind != JsonValueKind.Array ||
                        emptyDocument.RootElement.GetArrayLength() != 0)
                    {
                        _context.RecordFail(Module, getMethod, "securities list is not empty");
                        return;
                    }
                }

                _context.RecordPass(Module, getMethod, "empty list received");

                // Add a security.
                object addRequest = new
                {
                    name = setName,
                    securities = new[] { new { name = securityName } }
                };

                _context.PrintRequest(Module, addMethod, addRequest);
                string addResponse = _context.Client.ToolsCall(addMethod, addRequest);
                _context.PrintResponse(addResponse);

                if (!IsSuccessResponse(addResponse, out string addText) ||
                    string.IsNullOrEmpty(addText))
                {
                    _context.RecordFail(Module, addMethod, "add request failed");
                    return;
                }

                using (var addDocument = JsonDocument.Parse(addText))
                {
                    if (!addDocument.RootElement.TryGetProperty("added_count", out JsonElement addedCountElement)
                        || addedCountElement.GetInt32() != 1)
                    {
                        _context.RecordFail(Module, addMethod, "added_count is not 1");
                        return;
                    }
                }

                _context.RecordPass(Module, addMethod, "security added");

                // Verify the security is in the list.
                getResponse = _context.Client.ToolsCall(getMethod, getRequest);
                _context.PrintResponse(getResponse);

                if (!IsSuccessResponse(getResponse, out string listText) ||
                    string.IsNullOrEmpty(listText))
                {
                    _context.RecordFail(Module, getMethod, "failed to get securities list after add");
                    return;
                }

                bool found = false;

                using (var listDocument = JsonDocument.Parse(listText))
                {
                    foreach (JsonElement item in listDocument.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out JsonElement nameElement)
                            && nameElement.GetString() == securityName)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    _context.RecordFail(Module, getMethod, $"security '{securityName}' not found after add");
                    return;
                }

                _context.RecordPass(Module, getMethod, "security found in list");

                // Remove the security.
                object removeRequest = new
                {
                    name = setName,
                    securities = new[] { securityName }
                };

                _context.PrintRequest(Module, removeMethod, removeRequest);
                string removeResponse = _context.Client.ToolsCall(removeMethod, removeRequest);
                _context.PrintResponse(removeResponse);

                if (!IsSuccessResponse(removeResponse, out string removeText) ||
                    string.IsNullOrEmpty(removeText))
                {
                    _context.RecordFail(Module, removeMethod, "remove request failed");
                    return;
                }

                using (var removeDocument = JsonDocument.Parse(removeText))
                {
                    if (!removeDocument.RootElement.TryGetProperty("removed_count", out JsonElement removedCountElement)
                        || removedCountElement.GetInt32() != 1)
                    {
                        _context.RecordFail(Module, removeMethod, "removed_count is not 1");
                        return;
                    }
                }

                _context.RecordPass(Module, removeMethod, "security removed");

                // Clean up.
                _context.Client.ToolsCall(deleteMethod, new { name = setName });
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, getMethod, error.Message);

                // Attempt cleanup.
                try
                {
                    _context.Client.ToolsCall(deleteMethod, new { name = setName });
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void TestSetOnOff()
        {
            const string createMethod = "data_create_set";
            const string deleteMethod = "data_delete_set";
            const string getMethod = "data_set_settings_get";
            const string onMethod = "data_set_on";
            const string offMethod = "data_set_off";
            const string setName = "McpOnOffTestSet";
            const string serverType = "MoexDataServer";

            try
            {
                // Clean up a previously failed run.
                _context.Client.ToolsCall(deleteMethod, new { name = setName });

                // Activate the server so the set can be created.
                _context.Client.ToolsCall("server_management_activate", new { type = serverType });

                // Create a dedicated set.
                object createRequest = new
                {
                    name = setName,
                    source = serverType,
                    source_name = serverType,
                    timeframes = new[] { "Min1" },
                    date_from = "2024-01-01T00:00:00",
                    date_to = "2024-12-31T00:00:00"
                };

                _context.Client.ToolsCall(createMethod, createRequest);

                object getRequest = new { name = setName };

                // Verify initial regime is Off.
                string getResponse = _context.Client.ToolsCall(getMethod, getRequest);

                if (!IsSuccessResponse(getResponse, out string settingsText) ||
                    string.IsNullOrEmpty(settingsText))
                {
                    _context.RecordFail(Module, getMethod, "failed to get initial set settings");
                    return;
                }

                using (var settingsDocument = JsonDocument.Parse(settingsText))
                {
                    if (!settingsDocument.RootElement.TryGetProperty("regime", out JsonElement regimeElement)
                        || regimeElement.GetString() != "Off")
                    {
                        _context.RecordFail(Module, getMethod, "initial regime is not Off");
                        return;
                    }
                }

                // Turn the set on.
                object onRequest = new { name = setName };
                _context.PrintRequest(Module, onMethod, onRequest);
                string onResponse = _context.Client.ToolsCall(onMethod, onRequest);
                _context.PrintResponse(onResponse);

                if (!IsSuccessResponse(onResponse, out string onText) ||
                    string.IsNullOrEmpty(onText))
                {
                    _context.RecordFail(Module, onMethod, "turn on request failed");
                    return;
                }

                using (var onDocument = JsonDocument.Parse(onText))
                {
                    if (!onDocument.RootElement.TryGetProperty("regime", out JsonElement regimeElement)
                        || regimeElement.GetString() != "On")
                    {
                        _context.RecordFail(Module, onMethod, "regime is not On after turn on");
                        return;
                    }
                }

                _context.RecordPass(Module, onMethod, "set turned on");

                // Turn the set off.
                object offRequest = new { name = setName };
                _context.PrintRequest(Module, offMethod, offRequest);
                string offResponse = _context.Client.ToolsCall(offMethod, offRequest);
                _context.PrintResponse(offResponse);

                if (!IsSuccessResponse(offResponse, out string offText) ||
                    string.IsNullOrEmpty(offText))
                {
                    _context.RecordFail(Module, offMethod, "turn off request failed");
                    return;
                }

                using (var offDocument = JsonDocument.Parse(offText))
                {
                    if (!offDocument.RootElement.TryGetProperty("regime", out JsonElement regimeElement)
                        || regimeElement.GetString() != "Off")
                    {
                        _context.RecordFail(Module, offMethod, "regime is not Off after turn off");
                        return;
                    }
                }

                _context.RecordPass(Module, offMethod, "set turned off");

                // Verify via settings get.
                getResponse = _context.Client.ToolsCall(getMethod, getRequest);

                if (IsSuccessResponse(getResponse, out string finalSettingsText)
                    && !string.IsNullOrEmpty(finalSettingsText))
                {
                    using (var finalDocument = JsonDocument.Parse(finalSettingsText))
                    {
                        if (finalDocument.RootElement.TryGetProperty("regime", out JsonElement regimeElement)
                            && regimeElement.GetString() != "Off")
                        {
                            _context.RecordFail(Module, offMethod, "settings still show regime not Off");
                            return;
                        }
                    }
                }

                // Clean up.
                _context.Client.ToolsCall(deleteMethod, new { name = setName });
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, onMethod, error.Message);

                // Attempt cleanup.
                try
                {
                    _context.Client.ToolsCall(deleteMethod, new { name = setName });
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void TestDownloadFlow()
        {
            const string createMethod = "data_create_set";
            const string deleteMethod = "data_delete_set";
            const string addMethod = "data_set_securities_add";
            const string onMethod = "data_set_on";
            const string offMethod = "data_set_off";
            const string setStatusMethod = "data_get_set_status";
            const string securityStatusMethod = "data_get_security_status";
            const string setName = "McpDownloadFlowSet";
            const string serverType = "MoexDataServer";
            const string securityName = "SBER";
            const string timeFrame = "Min1";

            try
            {
                // Activate and connect the server so securities are available.
                _context.Client.ToolsCall("server_management_activate", new { type = serverType });
                _context.Client.ToolsCall("server_instance_connect", new { type = serverType });

                bool securitiesReady = false;
                DateTime waitStart = DateTime.Now;

                while ((DateTime.Now - waitStart).TotalSeconds < 120)
                {
                    string securitiesResponse = _context.Client.ToolsCall("server_instance_get_securities", new { type = serverType });

                    if (IsSuccessResponse(securitiesResponse, out string securitiesText)
                        && !string.IsNullOrEmpty(securitiesText))
                    {
                        using (var securitiesDocument = JsonDocument.Parse(securitiesText))
                        {
                            if (securitiesDocument.RootElement.TryGetProperty("count", out JsonElement countElement)
                                && countElement.GetInt32() > 0)
                            {
                                securitiesReady = true;
                                break;
                            }
                        }
                    }

                    Thread.Sleep(2000);
                }

                if (!securitiesReady)
                {
                    _context.RecordFail(Module, setStatusMethod, $"server '{serverType}' did not provide securities within 120 seconds");
                    return;
                }

                // Clean up a previously failed run.
                _context.Client.ToolsCall(deleteMethod, new { name = setName });

                DateTime dateTo = DateTime.Now.Date;
                DateTime dateFrom = dateTo.AddDays(-5);

                // Create a dedicated set with a short date range.
                object createRequest = new
                {
                    name = setName,
                    source = serverType,
                    source_name = serverType,
                    timeframes = new[] { timeFrame },
                    date_from = dateFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
                    date_to = dateTo.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                _context.Client.ToolsCall(createMethod, createRequest);

                // Add the security.
                object addRequest = new
                {
                    name = setName,
                    securities = new[] { new { name = securityName } }
                };

                _context.PrintRequest(Module, addMethod, addRequest);
                string addResponse = _context.Client.ToolsCall(addMethod, addRequest);
                _context.PrintResponse(addResponse);

                if (!IsSuccessResponse(addResponse, out string addText) ||
                    string.IsNullOrEmpty(addText))
                {
                    _context.RecordFail(Module, addMethod, "failed to add security for download flow test");
                    return;
                }

                using (var addDocument = JsonDocument.Parse(addText))
                {
                    if (!addDocument.RootElement.TryGetProperty("added_count", out JsonElement addedCountElement)
                        || addedCountElement.GetInt32() != 1)
                    {
                        _context.RecordFail(Module, addMethod, "added_count is not 1");
                        return;
                    }
                }

                _context.RecordPass(Module, addMethod, "security added for download flow test");

                // Turn the set on.
                object onRequest = new { name = setName };
                _context.PrintRequest(Module, onMethod, onRequest);
                string onResponse = _context.Client.ToolsCall(onMethod, onRequest);
                _context.PrintResponse(onResponse);

                if (!IsSuccessResponse(onResponse, out string onText) ||
                    string.IsNullOrEmpty(onText))
                {
                    _context.RecordFail(Module, onMethod, "failed to turn set on for download flow test");
                    return;
                }

                _context.RecordPass(Module, onMethod, "set turned on for download flow test");

                // Wait until data starts loading or finishes.
                bool downloadStarted = false;
                waitStart = DateTime.Now;
                string lastSetStatus = string.Empty;
                decimal lastSetPercent = 0;
                int lastObjectsCount = 0;

                while ((DateTime.Now - waitStart).TotalSeconds < 120)
                {
                    string setStatusResponse = _context.Client.ToolsCall(setStatusMethod, new { name = setName });
                    string securityStatusResponse = _context.Client.ToolsCall(securityStatusMethod, new
                    {
                        name = setName,
                        security = securityName,
                        timeframe = timeFrame
                    });

                    if (IsSuccessResponse(setStatusResponse, out string setStatusText)
                        && !string.IsNullOrEmpty(setStatusText))
                    {
                        using (var setStatusDocument = JsonDocument.Parse(setStatusText))
                        {
                            if (setStatusDocument.RootElement.TryGetProperty("status", out JsonElement statusElement))
                            {
                                lastSetStatus = statusElement.GetString() ?? string.Empty;
                            }

                            if (setStatusDocument.RootElement.TryGetProperty("percent_load", out JsonElement percentElement))
                            {
                                lastSetPercent = percentElement.GetDecimal();
                            }
                        }
                    }

                    if (IsSuccessResponse(securityStatusResponse, out string securityStatusText)
                        && !string.IsNullOrEmpty(securityStatusText))
                    {
                        using (var securityStatusDocument = JsonDocument.Parse(securityStatusText))
                        {
                            if (securityStatusDocument.RootElement.TryGetProperty("objects_count", out JsonElement objectsElement))
                            {
                                lastObjectsCount = objectsElement.GetInt32();
                            }
                        }
                    }

                    if (lastSetStatus == "Load")
                    {
                        downloadStarted = true;
                        break;
                    }

                    Thread.Sleep(5000);
                }

                if (!downloadStarted)
                {
                    _context.RecordFail(Module, setStatusMethod, $"download did not finish within 120 seconds. Last status: {lastSetStatus}, percent: {lastSetPercent}, objects: {lastObjectsCount}");
                    return;
                }

                _context.PrintRequest(Module, setStatusMethod, new { name = setName });
                _context.PrintResponse($"status={lastSetStatus}, percent_load={lastSetPercent}, objects_count={lastObjectsCount}");

                if (lastObjectsCount <= 0)
                {
                    _context.RecordFail(Module, securityStatusMethod, "objects_count is 0 after download finished");
                    return;
                }

                _context.RecordPass(Module, setStatusMethod, $"download finished: status={lastSetStatus}, percent={lastSetPercent}, objects={lastObjectsCount}");

                // Turn the set off and clean up.
                _context.Client.ToolsCall(offMethod, new { name = setName });
                _context.Client.ToolsCall(deleteMethod, new { name = setName });
            }
            catch (Exception error)
            {
                _context.PrintResponse("");
                _context.RecordFail(Module, setStatusMethod, error.Message);

                // Attempt cleanup.
                try
                {
                    _context.Client.ToolsCall(offMethod, new { name = setName });
                    _context.Client.ToolsCall(deleteMethod, new { name = setName });
                }
                catch
                {
                    // ignored
                }
            }
        }

        private bool IsSuccessResponse(string response, out string text)
        {
            text = string.Empty;

            try
            {
                using (var document = JsonDocument.Parse(response))
                {
                    JsonElement result = document.RootElement;

                    if (!result.TryGetProperty("IsError", out JsonElement isError) || isError.GetBoolean())
                    {
                        return false;
                    }

                    if (!result.TryGetProperty("Content", out JsonElement content) || content.GetArrayLength() == 0)
                    {
                        return false;
                    }

                    text = content[0].GetProperty("Text").GetString() ?? string.Empty;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private string EnsureFinamServerInstance()
        {
            try
            {
                // Activate the default Finam server instance (instance 0).
                // server_management_activate creates the server if it does not exist.
                string activateResponse = _context.Client.ToolsCall("server_management_activate",
                    new { type = TestServerType });

                using (var document = JsonDocument.Parse(activateResponse))
                {
                    JsonElement result = document.RootElement;

                    if (!result.GetProperty("IsError").GetBoolean())
                    {
                        string text = result.GetProperty("Content")[0].GetProperty("Text").GetString() ?? string.Empty;

                        using (var innerDocument = JsonDocument.Parse(text))
                        {
                            foreach (JsonElement item in innerDocument.RootElement.EnumerateArray())
                            {
                                string name = item.GetProperty("name").GetString() ?? string.Empty;

                                if (!string.IsNullOrEmpty(name))
                                {
                                    return name;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignored - fall back to the default instance name
            }

            // Assume the default instance exists
            return TestServerType;
        }
    }
}
