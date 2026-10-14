/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Logging;
using OsEngine.MCP.Json;
using OsEngine.UpdateModule;

namespace OsEngine.MCP.Modules
{
    /// <summary>
    /// MCP API handlers for the terminal auto-update module.
    /// update_apply downloads changed files and restarts the terminal via Updater.exe.
    /// Обработчики MCP API для модуля автообновления терминала.
    /// update_apply скачивает изменённые файлы и перезапускает терминал через Updater.exe.
    /// </summary>
    public class UpdateApi : IMcpToolProvider
    {
        #region Fields

        private readonly Action<string, object> _publishEvent;
        private readonly Action _stopTerminal;

        private static readonly object _applyLocker = new object();

        private static bool _applyInProgress;

        private const int UpdaterLivenessWindowMs = 1500;

        private readonly HttpClient _httpClient =
            new HttpClient(UpdateServerTls.CreatePinnedHandler()) { Timeout = TimeSpan.FromMinutes(2) };

        #endregion

        #region Events

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion

        #region Constructors

        public UpdateApi(Action<string, object> publishEvent, Action stopTerminal)
        {
            _publishEvent = publishEvent;
            _stopTerminal = stopTerminal;
        }

        #endregion

        #region Public methods

        public McpJsonRpcResponse Handle(McpJsonRpcRequest request)
        {
            McpJsonRpcResponse response = new McpJsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "update_check":
                        response.Result = Check();
                        break;

                    case "update_apply":
                        response.Result = Apply(request.Params).GetAwaiter().GetResult();
                        break;

                    default:
                        response.Error = new McpJsonRpcError
                        {
                            Code = -32601,
                            Message = $"Method '{request.Method}' not found in update API"
                        };
                        break;
                }
            }
            catch (Exception error)
            {
                response.Error = new McpJsonRpcError
                {
                    Code = -32603,
                    Message = error.Message
                };
            }

            return response;
        }

        public List<McpTool> GetTools()
        {
            return new List<McpTool>
            {
                new McpTool { Name = "update_check", Description = "Check the update server for new commits and changed files", InputSchema = new { type = "object", properties = new { }, required = new string[0] } },
                new McpTool { Name = "update_apply", Description = "Download and apply updates, then restart the terminal via Updater.exe (the terminal process stops). Params: restart (bool, default true) - stop the terminal after the updater starts", InputSchema = new { type = "object", properties = new { restart = new { type = "boolean", description = "Stop the terminal after the updater starts (default true)" } }, required = new string[0] } }
            };
        }

        #endregion

        #region Check

        private object Check()
        {
            try
            {
                UpdateResponse resp = RequestServerStatus();

                if (resp == null || resp.Success == false)
                {
                    return new
                    {
                        success = false,
                        status = "Disconnected",
                        error = resp?.Error ?? "No response from the update server"
                    };
                }

                FilesStateInfo info = ComputeFilesState(resp);

                return new
                {
                    success = true,
                    status = info.Status,
                    missedCommitsCount = resp.MissedCommitsCount,
                    newFiles = info.NewCount,
                    obsoleteFiles = info.ObsoleteCount,
                    removedFiles = info.RemovedCount,
                    updaterUpdateRequired = info.NeedUpdateUpdaterApp,
                    commits = (resp.Commits ?? new List<CommitDisplay>())
                        .Take(20)
                        .Select(c => new { c.Name, c.Number, c.Timestamp })
                        .ToList()
                };
            }
            catch (Exception error)
            {
                SendLog("update_check failed: " + error.Message, LogMessageType.Error);

                return new
                {
                    success = false,
                    status = "Disconnected",
                    error = error.Message
                };
            }
        }

        private UpdateResponse RequestServerStatus()
        {
            DateTime insideVersionDate = GetLastUpdateDate();

            using (SslStream stream = UpdateServerTls.ConnectPinned(UpdateServerTls.ServerHost, UpdateServerTls.UpdateProtocolPort, 5000))
            {
                string request = $"{{\"LastUpdateDate\":\"{insideVersionDate:yyyy-MM-ddTHH:mm:ssZ}\"}}";

                byte[] data = Encoding.UTF8.GetBytes(request);

                stream.Write(data, 0, data.Length);

                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }

                    string response = Encoding.UTF8.GetString(ms.ToArray());

                    if (string.IsNullOrEmpty(response))
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<UpdateResponse>(response);
                }
            }
        }

        private DateTime GetLastUpdateDate()
        {
            if (File.Exists(@"Engine\Updater\LastUpdatesInfo.txt"))
            {
                string time = File.ReadAllText(@"Engine\Updater\LastUpdatesInfo.txt");

                if (DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsedDate)
                    || DateTime.TryParse(time, out parsedDate))
                {
                    return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }
            }

            // модуль запущен первый раз - ориентир по дате сборки, как в MainWindow.GetUpdateInfo
            DateTime exeDate = File.GetLastWriteTimeUtc(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OsEngine.exe"));

            Directory.CreateDirectory(@"Engine\Updater");

            File.WriteAllText(@"Engine\Updater\LastUpdatesInfo.txt", exeDate.ToUniversalTime().ToString("O"));

            return exeDate;
        }

        private class FilesStateInfo
        {
            public string Status;
            public List<FileState> Files = new List<FileState>();
            public int NewCount;
            public int ObsoleteCount;
            public int RemovedCount;
            public bool NeedUpdateUpdaterApp;
        }

        private FilesStateInfo ComputeFilesState(UpdateResponse resp)
        {
            FilesStateInfo info = new FilesStateInfo();

            Dictionary<string, GithubFileInfo> filesTimeOnPc = ReadFilesVersionsTime(resp);

            if (resp.Files == null)
            {
                info.Status = "NoNeed";
                return info;
            }

            for (int i = 0; i < resp.Files.Count; i++)
            {
                FileState file = new FileState
                {
                    Name = resp.Files[i].Name,
                    Size = resp.Files[i].Size,
                    LastUpdate = resp.Files[i].LastUpdate,
                    Url = resp.Files[i].Url
                };

                if (filesTimeOnPc.ContainsKey(file.Name))
                {
                    file.CurrVersionTime = filesTimeOnPc[file.Name].LastUpdate;

                    if (filesTimeOnPc[file.Name].LastUpdate >= file.LastUpdate)
                    {
                        file.State = State.Actual;
                    }
                    else
                    {
                        file.State = State.Obsolete;
                        info.ObsoleteCount++;

                        if (file.Name.Contains("Updater.exe"))
                        {
                            info.NeedUpdateUpdaterApp = true;
                        }
                    }
                }
                else
                {
                    file.CurrVersionTime = DateTime.MinValue;
                    file.State = State.New;
                    info.NewCount++;
                }

                info.Files.Add(file);
            }

            // поиск удалённых файлов

            foreach (KeyValuePair<string, GithubFileInfo> pair in filesTimeOnPc)
            {
                if (resp.Files.Find(f => f.Name == pair.Key) == null
                    && pair.Key.Contains(".pdb") == false)
                {
                    info.Files.Add(new FileState
                    {
                        Name = pair.Key,
                        Size = pair.Value.Size,
                        LastUpdate = DateTime.MinValue,
                        CurrVersionTime = pair.Value.LastUpdate,
                        State = State.Removed
                    });

                    info.RemovedCount++;
                }
            }

            if (info.NewCount > 0 || info.ObsoleteCount > 0 || info.RemovedCount > 0
                || (resp.Commits != null && resp.Commits.Count > 0))
            {
                info.Status = "Available";
            }
            else
            {
                info.Status = "NoNeed";
            }

            return info;
        }

        private Dictionary<string, GithubFileInfo> ReadFilesVersionsTime(UpdateResponse resp)
        {
            Dictionary<string, GithubFileInfo> result = new Dictionary<string, GithubFileInfo>();

            if (File.Exists(@"Engine\Updater\FilesVersionsTime.txt") == false)
            {
                // первый запуск - записываем версии файлов как в MainWindow.WriteFilesVersionsTime
                WriteInitialFilesVersionsTime(resp);
            }

            string[] files = File.ReadAllLines(@"Engine\Updater\FilesVersionsTime.txt");

            for (int i = 0; i < files.Length; i++)
            {
                string[] fileAndTime = files[i].Split('#', StringSplitOptions.RemoveEmptyEntries);

                if (fileAndTime.Length < 3)
                {
                    continue;
                }

                GithubFileInfo fileInfo = new GithubFileInfo
                {
                    Name = fileAndTime[0],
                    LastUpdate = DateTime.Parse(fileAndTime[1]),
                    Size = long.Parse(fileAndTime[2])
                };

                result[fileInfo.Name] = fileInfo;
            }

            return result;
        }

        private void WriteInitialFilesVersionsTime(UpdateResponse resp)
        {
            if (resp?.Files == null)
            {
                return;
            }

            DateTime fileTime = GetLastUpdateDate();

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < resp.Files.Count; i++)
            {
                GithubFileInfo fileInfo = resp.Files[i];

                DateTime correctTime = fileInfo.LastUpdate > fileTime ? fileTime : fileInfo.LastUpdate;

                sb.AppendLine(fileInfo.Name + "#" + correctTime + "#" + fileInfo.Size);
            }

            Directory.CreateDirectory(@"Engine\Updater");

            File.WriteAllText(@"Engine\Updater\FilesVersionsTime.txt", sb.ToString());
        }

        #endregion

        #region Apply

        private async Task<object> Apply(JsonElement parameters)
        {
            bool restart = ParseRestart(parameters);

            lock (_applyLocker)
            {
                if (_applyInProgress)
                {
                    throw new ArgumentException("Update is already in progress");
                }

                _applyInProgress = true;
            }

            bool started = false;

            try
            {
                UpdateResponse resp = RequestServerStatus();

                if (resp == null || resp.Success == false)
                {
                    throw new ArgumentException("Update server error: " + (resp?.Error ?? "no response"));
                }

                FilesStateInfo info = ComputeFilesState(resp);

                if (info.Status != "Available")
                {
                    return new
                    {
                        success = true,
                        applied = false,
                        status = info.Status
                    };
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updaterExePath = Path.Combine(baseDir, "Updater.exe");

                if (File.Exists(updaterExePath) == false)
                {
                    throw new ArgumentException("Updater.exe not found next to OsEngine.exe");
                }

                // резервная копия настроек версий и всей папки программы

                string buildReservePath = $"Engine\\Updater\\Builds\\Build_{DateTime.Now:dd-MM-yyyy_HH-mm}";
                string tempDir = $"Engine\\Updater\\Temp";

                Directory.CreateDirectory(buildReservePath);
                Directory.CreateDirectory(tempDir);

                File.Copy(@"Engine\Updater\LastUpdatesInfo.txt", Path.Combine(buildReservePath, "LastUpdatesInfo.txt"), true);
                File.Copy(@"Engine\Updater\FilesVersionsTime.txt", Path.Combine(buildReservePath, "FilesVersionsTime.txt"), true);

                DateTime lastCommitTime = resp.Commits != null && resp.Commits.Count > 0
                    ? resp.Commits[0].Timestamp
                    : DateTime.UtcNow;

                File.WriteAllText(Path.Combine(tempDir, "LastUpdatesInfo.txt"), lastCommitTime.ToUniversalTime().ToString("O"));

                SendLog("Update apply: backing up the program folder", LogMessageType.System);

                CopyDirectory(baseDir, buildReservePath);

                List<FileState> newFiles = info.Files.FindAll(f => f.State == State.New);

                if (newFiles.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < newFiles.Count; i++)
                    {
                        sb.AppendLine(newFiles[i].Name);
                    }

                    File.WriteAllText(Path.Combine(buildReservePath, "NewFiles.txt"), sb.ToString());
                }

                // скачивание изменённых файлов в Temp

                List<FileState> filesForDownload = info.Files.FindAll(f => f.State == State.Obsolete || f.State == State.New);

                SendLog($"Update apply: downloading {filesForDownload.Count} files", LogMessageType.System);

                List<string> downloadedFiles = await DownloadChangedFiles(filesForDownload, tempDir);

                WriteTempFilesVersionsTime(tempDir, resp, info.Files, downloadedFiles);

                List<FileState> filesForDelete = info.Files.FindAll(f => f.State == State.Removed);

                if (filesForDelete.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < filesForDelete.Count; i++)
                    {
                        sb.AppendLine(filesForDelete[i].Name);
                    }

                    File.WriteAllText(Path.Combine(tempDir, "Files_For_Delete.txt"), sb.ToString());
                }

                // если изменился Updater.exe - сначала обновляем его

                if (info.NeedUpdateUpdaterApp)
                {
                    string newUpdaterPath = Path.Combine(tempDir, "-Updater.exe");

                    if (File.Exists(newUpdaterPath))
                    {
                        File.Copy(newUpdaterPath, updaterExePath, true);
                        File.Delete(newUpdaterPath);
                    }
                }

                TryRemoveOutdatedBuildsInCash();

                // запуск Updater.exe: он дожидается завершения процесса терминала и подменяет файлы

                UnblockFile(updaterExePath);

                string currentExeName = "OsEngine.exe";

                try
                {
                    currentExeName = Path.GetFileName(Environment.ProcessPath) ?? currentExeName;
                }
                catch
                {
                    // остаётся имя по умолчанию
                }

                string tempDirFullPath = Path.GetFullPath(tempDir);

                // targetDir без завершающего разделителя: в cmdline "...\OsEngine\" даёт экранированную
                // кавычку \" и Updater.exe не разбирает аргументы (Usage). Корень диска "C:\" обрезать
                // нельзя — "C:" drive-relative форма

                string updaterTargetDir = baseDir.TrimEnd('\\', '/');

                if (updaterTargetDir.Length == 2 && updaterTargetDir[1] == ':')
                {
                    updaterTargetDir = baseDir;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = $"\"update\" \"{updaterTargetDir}\" \"{tempDirFullPath}\" {Environment.ProcessId} \"{currentExeName}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using (Process updaterProcess = Process.Start(startInfo))
                {
                    started = true;

                    // апдейтер по контракту ждёт выхода процесса терминала, а терминал сейчас жив:
                    // выход процесса в окне = сбой (на ошибке апдейтер может выйти и с кодом 0).
                    // null Process и исключения хэндла = «сбой не обнаружен»: стоп как раньше

                    bool updaterDied = false;
                    string updaterExit = "";

                    try
                    {
                        if (updaterProcess != null && updaterProcess.WaitForExit(UpdaterLivenessWindowMs))
                        {
                            updaterDied = true;

                            try
                            {
                                updaterExit = " (exit code " + updaterProcess.ExitCode + ")";
                            }
                            catch
                            {
                                // код недоступен — в текст ошибки не попадает
                            }
                        }
                    }
                    catch
                    {
                        // мёртвый/недоступный хэндл: сбой не подтверждён, деградация к штатной остановке

                        updaterDied = false;
                    }

                    if (updaterDied)
                    {
                        started = false;

                        string failure = "Updater exited immediately" + updaterExit
                            + ". The terminal was not stopped. Backup kept: " + Path.GetFullPath(buildReservePath);

                        SendLog("update_apply: " + failure, LogMessageType.Error);

                        throw new InvalidOperationException(failure);
                    }
                }

                SendLog("Update apply: updater started, terminal is shutting down", LogMessageType.System);

                try
                {
                    _publishEvent?.Invoke("update.apply_started", new
                    {
                        downloadedFiles = downloadedFiles.Count,
                        time = DateTime.Now
                    });
                }
                catch
                {
                    // событие best-effort
                }

                bool willStop = restart && _stopTerminal != null;

                if (willStop)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            // задержка, чтобы ответ MCP успел уйти клиенту до остановки терминала
                            Thread.Sleep(800);
                            _stopTerminal();
                        }
                        catch (Exception error)
                        {
                            SendLog("Terminal stop after update failed: " + error, LogMessageType.Error);
                        }
                    });
                }

                return new
                {
                    success = true,
                    applied = true,
                    downloadedFiles = downloadedFiles.Count,
                    filesForDelete = filesForDelete.Count,
                    restartTerminal = willStop,
                    message = willStop
                        ? "Updater started. The terminal is shutting down for the update"
                        : "Updater started. Stop the terminal manually to complete the update"
                };
            }
            catch (Exception error)
            {
                SendLog("update_apply failed: " + error, LogMessageType.Error);
                throw;
            }
            finally
            {
                if (started == false)
                {
                    _applyInProgress = false;
                }
            }
        }

        private bool ParseRestart(JsonElement parameters)
        {
            if (parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("restart", out JsonElement restartElement)
                && (restartElement.ValueKind == JsonValueKind.True || restartElement.ValueKind == JsonValueKind.False))
            {
                return restartElement.GetBoolean();
            }

            return true;
        }

        private async Task<List<string>> DownloadChangedFiles(List<FileState> files, string tempDir)
        {
            List<string> downloadedFiles = new List<string>();

            Directory.CreateDirectory(tempDir);

            for (int i = 0; i < files.Count; i++)
            {
                string tempFileName = files[i].Name.Replace("\\", "-"); // в имени файла - указание на подпапки

                string tempPath = Path.Combine(tempDir, tempFileName);

                string secureUrl = UpdateServerTls.GetSecureFileUrl(files[i].Url); // скачиваем только по HTTPS

                using (HttpResponseMessage response = await _httpClient.GetAsync(secureUrl))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLog($"Update apply: file not downloaded ({response.StatusCode}): {files[i].Name}", LogMessageType.Error);
                        continue;
                    }

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                    using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        contentStream.CopyTo(fileStream);
                    }
                }

                // проверяем целостность: файл должен существовать и сойтись по размеру

                FileInfo downloaded = new FileInfo(tempPath);

                if (downloaded.Exists && downloaded.Length == files[i].Size)
                {
                    downloadedFiles.Add(files[i].Name);
                }
                else
                {
                    SendLog("Update apply: size mismatch: " + files[i].Name, LogMessageType.Error);

                    if (downloaded.Exists)
                    {
                        downloaded.Delete();
                    }
                }
            }

            return downloadedFiles;
        }

        private void CopyDirectory(string sourceDirName, string destDirName)
        {
            Directory.CreateDirectory(destDirName);

            string[] files = Directory.GetFiles(sourceDirName);

            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Contains("Updater")) // пропускаем Updater.exe
                {
                    continue;
                }

                string targetFile = Path.Combine(destDirName, Path.GetFileName(files[i]));

                File.Copy(files[i], targetFile, true);
            }

            string[] folders = Directory.GetDirectories(sourceDirName);

            for (int i = 0; i < folders.Length; i++)
            {
                if (folders[i].Contains("\\Engine") || folders[i].Contains("\\Data")) // папки, которые не обновляются
                {
                    continue;
                }

                string targetSubdir = Path.Combine(destDirName, Path.GetFileName(folders[i]));

                CopyDirectory(folders[i], targetSubdir);
            }
        }

        private void WriteTempFilesVersionsTime(string tempDir, UpdateResponse resp, List<FileState> filesInDebug, List<string> downloadedFiles)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < resp.Files.Count; i++)
            {
                GithubFileInfo fileInfo = resp.Files[i];

                FileState state = filesInDebug.Find(f => f.Name == fileInfo.Name);

                if (state == null)
                {
                    continue;
                }

                if (state.State == State.Actual)
                {
                    // в этом раунде не качали - оставляем записанное ранее время
                    sb.AppendLine(fileInfo.Name + "#" + state.CurrVersionTime + "#" + fileInfo.Size);
                }
                else if (downloadedFiles.Contains(fileInfo.Name))
                {
                    // честно скачан - записываем серверное время
                    sb.AppendLine(fileInfo.Name + "#" + fileInfo.LastUpdate + "#" + fileInfo.Size);
                }
                else if (state.State == State.Obsolete)
                {
                    // не скачался - оставляем старое время, повторит при следующей проверке
                    sb.AppendLine(fileInfo.Name + "#" + state.CurrVersionTime + "#" + fileInfo.Size);
                }
                // State.New и не скачался - в запись не попадает, в следующий раз снова будет New
            }

            File.WriteAllText(Path.Combine(tempDir, "FilesVersionsTime.txt"), sb.ToString());
        }

        private void TryRemoveOutdatedBuildsInCash()
        {
            try
            {
                SaveVersionsType versionsSaveType = SaveVersionsType.All;

                if (File.Exists(@"Engine\Updater\UpdaterSettings.txt"))
                {
                    Enum.TryParse(File.ReadAllLines(@"Engine\Updater\UpdaterSettings.txt").FirstOrDefault(), out versionsSaveType);
                }

                if (versionsSaveType == SaveVersionsType.All)
                {
                    return;
                }

                string buildPath = $"Engine\\Updater\\Builds\\";

                string[] directories = Directory.GetDirectories(buildPath);

                if (directories.Length <= 5)
                {
                    return;
                }

                List<DirectoryInfo> directoriesInfo = directories
                    .Select(d => new DirectoryInfo(d))
                    .OrderBy(x => x.CreationTimeUtc)
                    .Reverse()
                    .ToList();

                for (int i = 5; i < directoriesInfo.Count; i++)
                {
                    try
                    {
                        Directory.Delete(directoriesInfo[i].FullName, true);
                    }
                    catch (Exception error)
                    {
                        SendLog(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLog(error.ToString(), LogMessageType.Error);
            }
        }

        private void UnblockFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // удаляем поток Zone.Identifier (MOTW)
                    string zoneIdentifierPath = filePath + ":Zone.Identifier";

                    if (File.Exists(zoneIdentifierPath))
                    {
                        File.Delete(zoneIdentifierPath);
                    }
                }
            }
            catch (Exception error)
            {
                SendLog($"Unblock file {filePath} failed: {error.Message}", LogMessageType.Error);
            }
        }

        #endregion

        #region Log

        private void SendLog(string message, LogMessageType type)
        {
            try
            {
                NewLogMessageEvent?.Invoke(message, type);
            }
            catch
            {
                // не мешаем основному потоку
            }

            try
            {
                // дублируем в лог обновлятора - он читается после перезапуска
                string fullMsg = $"{DateTime.Now:G}: {message}\n";

                Directory.CreateDirectory(@"Engine\Log");

                File.AppendAllText($"Engine\\Log\\UpdaterLog_{DateTime.Now:dd-MM-yyyy}.txt", fullMsg);
            }
            catch
            {
                // не мешаем основному потоку
            }
        }

        #endregion
    }
}
