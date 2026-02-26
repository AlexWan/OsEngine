/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace OsEngine.UpdateModule
{
    public partial class UpdateModuleUi : Window
    {
        private UpdateResponse _serverResp;
        private UpdaterStatus _status;
        private List<FileState> _filesInDebug;
        private Dictionary<string, GithubFileInfo> _filesTimeOnPC;
        private readonly CancellationTokenSource _updCts = new();
        private bool _needUpdateUpdaterApp;
        private int _modifiedFilesCount;

        private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });

        public UpdateModuleUi(UpdateResponse response)
        {
            InitializeComponent();

            _serverResp = response;

            Title = OsLocalization.Updater.TitleUpdater;
            ButtonUpdRequest.Content = OsLocalization.Updater.ButtonRequest;
            TabFiles.Header = OsLocalization.Updater.TabItemFiles;
            LabelFiles.Content = OsLocalization.Updater.Label1;
            FilesDataGrid.Columns[0].Header = OsLocalization.Updater.GridColumn1;
            FilesDataGrid.Columns[1].Header = OsLocalization.Updater.GridColumn2;
            FilesDataGrid.Columns[2].Header = OsLocalization.Updater.GridColumn3;
            FilesDataGrid.Columns[3].Header = OsLocalization.Updater.GridColumn4;
            FilesDataGrid.Columns[4].Header = OsLocalization.Updater.GridColumn5;
            ButtonUpdate.Content = OsLocalization.Updater.ButtonUpdate;

            TabCommits.Header = OsLocalization.Updater.TabItemCommits;
            LabelCommits.Content = OsLocalization.Updater.Label2;
            CommitsDataGrid.Columns[0].Header = OsLocalization.Updater.GridColumn6;
            CommitsDataGrid.Columns[1].Header = OsLocalization.Updater.GridColumn7;

            TabVersions.Header = OsLocalization.Updater.TabItemVers;
            LabelVers.Content = OsLocalization.Updater.Label3;
            VersionsDataGrid.Columns[0].Header = OsLocalization.Updater.GridColumn7;
            VersionsDataGrid.Columns[1].Header = OsLocalization.Updater.GridColumn8;
            VersionsDataGrid.Columns[2].Header = OsLocalization.Updater.GridColumn9;
            VersionsDataGrid.Columns[3].Header = OsLocalization.Updater.GridColumn10;

            LabelLog.Content = OsLocalization.Updater.Label4;
            LogsDataGrid.Columns[0].Header = OsLocalization.Updater.GridColumn7;
            LogsDataGrid.Columns[1].Header = OsLocalization.Updater.GridColumn11;

            Loaded += UpdateModuleUi_Loaded;
            Closed += UpdateModuleUi_Closed;
        }

        private void UpdateModuleUi_Closed(object sender, EventArgs e)
        {
            _updCts.Cancel();
            _updCts.Dispose();

            _serverResp = null;
            _filesInDebug = null;
            _filesTimeOnPC = null;

            _httpClient.Dispose();
        }

        private void UpdateModuleUi_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                VersionsDataGrid.ItemsSource = GetLastVersions();

                if (File.Exists(@"Engine\Updater\FilesVersionsTime.txt"))
                {
                    GetFilesTimeOnPC();
                }

                if (_serverResp == null)
                {
                    _status = UpdaterStatus.Disconnected;

                    SaveLogMessage(OsLocalization.Updater.Message1);
                    return;
                }
                else if (!_serverResp.Success)
                {
                    _status = UpdaterStatus.Disconnected;

                    SaveLogMessage($"{OsLocalization.Updater.Message2}: {_serverResp.Error}.");
                    return;
                }

                // сформировать данные для таблиц
                _filesInDebug = GetFilesState(_serverResp.Files);

                _filesInDebug.Sort((x, y) => x.State.CompareTo(y.State));

                FilesDataGrid.ItemsSource = _filesInDebug;

                CommitsDataGrid.ItemsSource = _serverResp.Commits;

                TabCommits.Header = $"{OsLocalization.Updater.TabItemCommits} ({_serverResp.Commits.Count})";

                SaveLogMessage($"{OsLocalization.Updater.Message3.Split('#')[0]} {_serverResp.Commits.Count} {OsLocalization.Updater.Message3.Split('#')[1]}");
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message4}: {ex.Message}");
            }
        }

        private void GetFilesTimeOnPC()
        {
            _filesTimeOnPC = new Dictionary<string, GithubFileInfo>();

            try
            {
                string[] files = File.ReadAllLines(@"Engine\Updater\FilesVersionsTime.txt");

                for (int i = 0; i < files.Length; i++)
                {
                    string[] fileAndTime = files[i].Split('#', StringSplitOptions.RemoveEmptyEntries);

                    GithubFileInfo fileInfo = new GithubFileInfo();

                    fileInfo.Name = fileAndTime[0];
                    fileInfo.LastUpdate = DateTime.Parse(fileAndTime[1]);
                    fileInfo.Size = int.Parse(fileAndTime[2]);

                    _filesTimeOnPC.Add(fileInfo.Name, fileInfo);
                }
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message55}: Engine\\Updater\\FilesVersionsTime.txt: {ex.Message}");
            }
        }


        #region Last versions
        private List<BuildVersion> GetLastVersions()
        {
            List<BuildVersion> builds = [];

            try
            {
                if (!Directory.Exists(@"Engine\Updater\Builds"))
                {
                    Directory.CreateDirectory(@"Engine\Updater\Builds");
                    SaveLogMessage(OsLocalization.Updater.Message5);
                    return builds;
                }

                string[] versions = Directory.GetDirectories(@"Engine\Updater\Builds");

                if (versions.Length > 0)
                {
                    TabVersions.Header = $"{OsLocalization.Updater.TabItemVers} ({versions.Length})";
                }

                for (int i = 0; i < versions.Length; i++)
                {
                    if (!CheckFolderContents(versions[i]))
                        continue;

                    BuildVersion build = new BuildVersion();
                    build.Path = versions[i];
                    build.VersionTime = Directory.GetCreationTime(versions[i]);
                    build.Open = OsLocalization.Updater.GridColumn9;
                    build.RollBack = OsLocalization.Updater.GridColumn10;
                    build.OpenButtonText = OsLocalization.Updater.GridColumn9;
                    build.RollbackButtonText = OsLocalization.Updater.GridColumn10;

                    builds.Add(build);
                }

                return builds;
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message6}: {ex.Message}");
                return builds;
            }
        }

        private bool CheckFolderContents(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath);
            string[] folders = Directory.GetDirectories(folderPath);

            if (files.Length + folders.Length > 0)
            {
                return true;
            }
            else
            {
                Directory.Delete(folderPath, true);
                return false;
            }
        }

        // Выделение строки по ПКМ
        private void VersionsDataGrid_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;

            // Получаем элемент под курсором мыши
            var hitTestResult = VisualTreeHelper.HitTest(dataGrid, e.GetPosition(dataGrid));
            var row = GetParent<DataGridRow>(hitTestResult.VisualHit);

            if (row != null)
            {
                dataGrid.SelectedItem = row.Item;
            }
        }

        private T GetParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }

        // Кнопка "Открыть"
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string path = button.Tag as string;
            if (string.IsNullOrEmpty(path)) return;

            OpenFolder(path);
        }

        // Кнопка "Откатить"
        private void RollbackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button button) return;

                string path = button.Tag as string;

                if (string.IsNullOrEmpty(path)) return;

                AcceptDialogUi ui = new AcceptDialogUi($"{OsLocalization.Updater.Message7} {path.Substring(path.Length - 16)}\n{OsLocalization.Updater.Message8}");
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                SaveLogMessage($"{OsLocalization.Updater.Message9}: {path.Substring(path.Length - 16)}");

                //проверка содержимого резервной папки, на случай отката одного Updater.exe
                string[] files = Directory.GetFiles(path);

                if (files.Length == 3)
                {
                    bool isWrongFile = false;

                    for (int i = 0; i < files.Length; i++)
                    {
                        string fileName = Path.GetFileName(files[i]);

                        if (fileName != "Updater.exe" && fileName != "LastUpdatesInfo.txt" && fileName != "FilesVersionsTime.txt")
                        {
                            isWrongFile = true;
                            break;
                        }
                    }

                    if (!isWrongFile) // все файлы, которые должны быть только при обновлении одного Updater.exe
                    {
                        for (int i = 0; i < files.Length; i++)
                        {
                            string fileName = Path.GetFileName(files[i]);

                            if (fileName == "Updater.exe")
                            {
                                string updaterExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");

                                File.Copy(files[i], updaterExePath, true);
                            }
                            else if (fileName == "LastUpdatesInfo.txt")
                            {
                                // обратно записываем время до обновления
                                File.Copy(files[i], @"Engine\Updater\LastUpdatesInfo.txt", true);
                            }
                            else // "FilesVersionsTime.txt"
                            {
                                // обратно записываем версии файлов
                                File.Copy(files[i], @"Engine\Updater\FilesVersionsTime.txt", true);
                            }
                        }

                        SaveLogMessage(OsLocalization.Updater.Message57);

                        CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Updater.Message58);
                        boxUi.ShowDialog();
                        return;
                    }
                }

                RollbackChanges(path);
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message13}: {ex.Message}");
            }
        }

        // Метод открытия папки
        private void OpenFolder(string path)
        {
            try
            {
                // Проверяем, существует ли путь
                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", path);
                }
                else if (File.Exists(path))
                {
                    // Если это файл, открываем его родительскую папку
                    string directory = Path.GetDirectoryName(path);
                    if (Directory.Exists(directory))
                    {
                        Process.Start("explorer.exe", directory);
                    }
                    else
                    {
                        SaveLogMessage($"{OsLocalization.Updater.Message10}: {directory}");
                    }
                }
                else
                {
                    SaveLogMessage($"{OsLocalization.Updater.Message11}: {path}");
                }
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message12}: {ex.Message}");
            }
        }

        private void RollbackChanges(string needVersionPath)
        {
            try
            {
                string currentExePath = "";

                using (Process process = Process.GetCurrentProcess())
                {
                    try
                    {
                        currentExePath = process.MainModule.FileName;
                    }
                    catch
                    {
                        currentExePath = Environment.ProcessPath;
                    }
                }

                string currentDir = Path.GetDirectoryName(currentExePath);
                string updaterExePath = Path.Combine(currentDir, "Updater.exe");

                // передать в Updater пути и флаг отката
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = $"\"rollback\" \"{currentDir}\" \"{needVersionPath}\" {Environment.ProcessId} \"{Path.GetFileName(currentExePath)}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                // Запускаем Updater и закрываем текущее приложение
                Process.Start(startInfo);

                // Закрываем приложение
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message13}: {ex.Message}");
            }
        }

        #endregion

        #region Files and commits

        private List<FileState> GetFilesState(List<GithubFileInfo> filesFromServer)
        {
            List<FileState> result = [];

            try
            {
                if (_filesTimeOnPC == null || _filesTimeOnPC.Count == 0)
                {
                    SaveLogMessage(OsLocalization.Updater.Message14);
                    return result;
                }

                int changedFilesCount = 0;

                for (int i = 0; i < filesFromServer.Count; i++)
                {
                    FileState file = new FileState();

                    file.Name = filesFromServer[i].Name;
                    file.Size = filesFromServer[i].Size;
                    file.LastUpdate = filesFromServer[i].LastUpdate;
                    file.Url = filesFromServer[i].Url;

                    if (_filesTimeOnPC.ContainsKey(file.Name))
                    {
                        if (_filesTimeOnPC[file.Name].LastUpdate >= file.LastUpdate)
                        {
                            file.CurrVersionTime = _filesTimeOnPC[file.Name].LastUpdate;
                            file.State = State.Actual;
                        }
                        else if (_filesTimeOnPC[file.Name].LastUpdate < file.LastUpdate) // на сервере файл новее
                        {
                            file.CurrVersionTime = _filesTimeOnPC[file.Name].LastUpdate;
                            file.State = State.Obsolete;

                            if (file.Name.Contains("Updater.exe"))
                            {
                                _needUpdateUpdaterApp = true;
                            }

                            changedFilesCount++;
                        }
                    }
                    else
                    {
                        file.CurrVersionTime = DateTime.MinValue;
                        file.State = State.New;

                        changedFilesCount++;
                    }

                    result.Add(file);
                }

                string logMsg = $"{changedFilesCount} {OsLocalization.Updater.Message15}";

                // поиск удалённых файлов

                int deletedFilesCount = 0;

                var enumerator = _filesTimeOnPC.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    if (filesFromServer.Find(f => f.Name == enumerator.Current.Key) == null && !enumerator.Current.Key.Contains(".pdb"))
                    {
                        FileState file = new FileState();
                        file.Name = enumerator.Current.Key;
                        file.Size = enumerator.Current.Value.Size;
                        file.LastUpdate = DateTime.MinValue;
                        file.CurrVersionTime = enumerator.Current.Value.LastUpdate;
                        file.State = State.Removed;

                        result.Add(file);

                        deletedFilesCount++;
                    }
                }

                if (result.Count > 0)
                {
                    if (result.Find(f => f.State == State.Obsolete || f.State == State.Removed || f.State == State.New) != null)
                    {
                        _status = UpdaterStatus.Available;
                    }
                    else if (_serverResp.Commits.Count > 0)
                    {
                        _status = UpdaterStatus.Available;
                    }
                    else
                    {
                        _status = UpdaterStatus.NoNeed;
                    }

                    if (deletedFilesCount > 0)
                    {
                        logMsg += $" {deletedFilesCount} {OsLocalization.Updater.Message16}";
                    }

                    _modifiedFilesCount = changedFilesCount + deletedFilesCount;

                    TabFiles.Header = $"{OsLocalization.Updater.TabItemFiles} ({_modifiedFilesCount})";

                    SaveLogMessage(logMsg);
                }

                return result;
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message17}: {ex.Message}");
                return result;
            }
        }

        private void ButtonUpdRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveLogMessage(OsLocalization.Updater.Message18);

                // если прошло менее 1 мин с прошлого запроса, не вызываем
                if (_serverResp != null && DateTime.UtcNow < _serverResp.ServerTime.AddMinutes(1))
                {
                    CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Updater.Message19);
                    boxUi.ShowDialog();
                    return;
                }

                DateTime insideVersionDate = DateTime.UtcNow;

                if (File.Exists(@"Engine\Updater\LastUpdatesInfo.txt"))
                {
                    // взять из файла последний коммит
                    string time = File.ReadAllText(@"Engine\Updater\LastUpdatesInfo.txt");

                    if (!DateTime.TryParse(time, out insideVersionDate))
                    {
                        SaveLogMessage(OsLocalization.Updater.Message20);
                        return;
                    }
                }

                string ip = "185.186.143.9";
                int port = 49152;

                using (TcpClient client = new TcpClient())
                {
                    SaveLogMessage($"{OsLocalization.Updater.Message21} {ip}:{port}...");

                    client.Connect(ip, port);

                    if (client.Connected)
                    {
                        SaveLogMessage(OsLocalization.Updater.Message22);

                        string request = $"{{\"LastUpdateDate\":\"{insideVersionDate:yyyy-MM-ddTHH:mm:ss}\"}}";

                        byte[] data = Encoding.UTF8.GetBytes(request);

                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);

                        SaveLogMessage($"{OsLocalization.Updater.Message23}: {insideVersionDate:yyyy-MM-dd HH:mm:ss}");

                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;

                            // Читаем пока есть данные
                            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                ms.Write(buffer, 0, bytesRead);
                            }

                            string response = Encoding.UTF8.GetString(ms.ToArray());

                            if (!string.IsNullOrEmpty(response))
                            {
                                SaveLogMessage(OsLocalization.Updater.Message24);

                                UpdateResponse specResponse = JsonSerializer.Deserialize<UpdateResponse>(response);

                                if (specResponse != null)
                                {
                                    _serverResp = specResponse;

                                    _filesInDebug = GetFilesState(_serverResp.Files);

                                    _filesInDebug.Sort((x, y) => x.State.CompareTo(y.State));

                                    FilesDataGrid.ItemsSource = _filesInDebug;

                                    CommitsDataGrid.ItemsSource = _serverResp.Commits;

                                    TabCommits.Header = $"{OsLocalization.Updater.TabItemCommits} ({_serverResp.Commits.Count})";

                                    SaveLogMessage($"{_serverResp.Commits.Count} {OsLocalization.Updater.Message25}");
                                }
                            }
                            else
                            {
                                SaveLogMessage(OsLocalization.Updater.Message26);
                                _status = UpdaterStatus.Disconnected;
                            }
                        }
                    }
                    else
                    {
                        SaveLogMessage(OsLocalization.Updater.Message27);
                        _status = UpdaterStatus.Disconnected;
                    }
                }
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message28}: {ex.Message}");
                _status = UpdaterStatus.Disconnected;
            }
        }
        #endregion

        #region Update process

        public bool IsUpdated = false;

        private async void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            SaveLogMessage(OsLocalization.Updater.Message29);

            if (_status == UpdaterStatus.Available)
            {
                AcceptDialogUi ui = new AcceptDialogUi($"{OsLocalization.Updater.Message8}\n{OsLocalization.Updater.Message30}");
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                // процесс обновления
                SaveLogMessage(OsLocalization.Updater.Message31);

                IsUpdated = true;

                // Отключаем кнопки на время обновления
                ButtonUpdate.IsEnabled = false;
                ButtonUpdRequest.IsEnabled = false;

                string buildReservePath = $"Engine\\Updater\\Builds\\Build_{DateTime.Now:dd-MM-yyyy_HH-mm}";
                Directory.CreateDirectory(buildReservePath);

                string tempDir = $"Engine\\Updater\\Temp";
                Directory.CreateDirectory(tempDir);

                string updaterExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");

                // Добавляем файл с временем обновления на случай отката
                string updTempPath = Path.Combine(buildReservePath, "LastUpdatesInfo.txt");

                File.Copy(@"Engine\Updater\LastUpdatesInfo.txt", updTempPath, true);

                // и запись о текущей версии файлов
                string filesTempPath = Path.Combine(buildReservePath, "FilesVersionsTime.txt");

                File.Copy(@"Engine\Updater\FilesVersionsTime.txt", filesTempPath, true);

                // также в папку Temp кладём файл с временем последнего коммита  на сервере,
                // чтобы после успешного обновления при следующем запуске от него смотреть изменения
                File.WriteAllText(tempDir + "\\LastUpdatesInfo.txt", _serverResp.Commits[0].Timestamp.ToString("G"));

                //файлы с новым временем в папку Temp 
                WriteFilesVersionsTime(tempDir);

                // Если изменился только Updater.exe
                if (_needUpdateUpdaterApp && _modifiedFilesCount == 1)
                {
                    bool updSucces = await UpdateOnlyUpdaterExe(buildReservePath, tempDir, updaterExePath);

                    if (updSucces)
                    {
                        Directory.Delete(tempDir, true);
                        SaveLogMessage(OsLocalization.Updater.Message41);
                    }

                    ButtonUpdate.IsEnabled = true;
                    ButtonUpdRequest.IsEnabled = true;

                    CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Updater.Message58);
                    boxUi.ShowDialog();

                    return;
                }

                SaveLogMessage(OsLocalization.Updater.Message32);

                try
                {
                    // делает резервную копию всей папки Debug в папку с резервными копиями
                    await Task.Run(async () =>
                    {
                        try
                        {
                            await CopyDirectory(AppDomain.CurrentDomain.BaseDirectory, buildReservePath, _updCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            SaveLogMessage(OsLocalization.Updater.Message33);
                            Directory.Delete(buildReservePath, true);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            SaveLogMessage($"{OsLocalization.Updater.Message34}: {ex.Message}");
                            Directory.Delete(buildReservePath, true);
                            throw;
                        }
                    }, _updCts.Token);


                    List<FileState> newFiles = _filesInDebug.FindAll(f => f.State == State.New);

                    if (newFiles.Count > 0)
                    {
                        // записать новые файлы, чтобы можно было удалить при откате
                        string newFilesPath = Path.Combine(buildReservePath, "NewFiles.txt");

                        StringBuilder sb = new StringBuilder();

                        for (int i = 0; i < newFiles.Count; i++)
                        {
                            FileState file = newFiles[i];
                            sb.AppendLine(file.Name);
                        }

                        File.WriteAllText(newFilesPath, sb.ToString());
                    }

                    SaveLogMessage(OsLocalization.Updater.Message35);

                    // скачивает поочереди все нужные файлы, аккуратно кладя их в папку Temp
                    List<FileState> filesForDownload = _filesInDebug.FindAll(f => f.State == State.Obsolete || f.State == State.New);

                    if (filesForDownload.Count > 0)
                    {
                        // Ожидаем скачивание файлов
                        await Task.Run(async () =>
                        {
                            try
                            {
                                await DownloadChangedFiles(filesForDownload, tempDir, _updCts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                SaveLogMessage(OsLocalization.Updater.Message36);
                                Directory.Delete(tempDir, true);
                                throw;
                            }
                            catch (Exception ex)
                            {
                                SaveLogMessage($"{OsLocalization.Updater.Message37}: {ex.Message}");
                                Directory.Delete(tempDir, true);
                                throw;
                            }
                        }, _updCts.Token);

                        SaveLogMessage(OsLocalization.Updater.Message38);
                    }

                    List<FileState> filesForDelete = _filesInDebug.FindAll(f => f.State == State.Removed);

                    if (filesForDelete.Count > 0) // сохраняем список файлов, подлежащих удалению
                    {
                        StringBuilder sb = new StringBuilder();

                        for (int i = 0; i < filesForDelete.Count; i++)
                        {
                            FileState file = filesForDelete[i];
                            sb.AppendLine(file.Name);
                        }

                        File.WriteAllText(tempDir + "\\Files_For_Delete.txt", sb.ToString());

                        SaveLogMessage(OsLocalization.Updater.Message39);
                    }

                    if (!File.Exists(updaterExePath))
                    {
                        SaveLogMessage(OsLocalization.Updater.Message40);
                        return;
                    }

                    // Если Updater.exe изменился, сначала обновляем его              
                    if (_needUpdateUpdaterApp)
                    {
                        if (File.Exists(tempDir + "\\-Updater.exe"))
                        {
                            File.Copy(tempDir + "\\-Updater.exe", updaterExePath, true);

                            File.Delete(tempDir + "\\-Updater.exe");

                            SaveLogMessage(OsLocalization.Updater.Message41);
                        }
                        else
                        {
                            SaveLogMessage(OsLocalization.Updater.Message56);
                        }
                    }

                    SaveLogMessage(OsLocalization.Updater.Message42);

                    StartUpdateApp(tempDir);

                }
                catch (Exception ex)
                {
                    string errMsg = $"{OsLocalization.Updater.Message43}: {ex.Message}";
                    SaveLogMessage(errMsg);
                    CustomMessageBoxUi boxUi = new CustomMessageBoxUi(errMsg);
                    boxUi.ShowDialog();
                }
                finally
                {
                    // Включаем обратно
                    ButtonUpdate.IsEnabled = true;
                    ButtonUpdRequest.IsEnabled = true;
                }
            }
            else if (_status == UpdaterStatus.NoNeed)
            {
                string stateMsg = OsLocalization.Updater.Message44;
                SaveLogMessage(stateMsg);
                CustomMessageBoxUi boxUi = new CustomMessageBoxUi(stateMsg);
                boxUi.ShowDialog();
            }
            else if (_status == UpdaterStatus.Disconnected)
            {
                string stateMsg = OsLocalization.Updater.Message45;
                SaveLogMessage(stateMsg);
                CustomMessageBoxUi boxUi = new CustomMessageBoxUi(stateMsg);
                boxUi.ShowDialog();
            }
        }

        private async Task<bool> UpdateOnlyUpdaterExe(string buildReservePath, string tempDir, string updaterExePath)
        {
            try
            {
                List<FileState> filesForDownload = _filesInDebug.FindAll(f => f.State == State.Obsolete);

                if (filesForDownload.Count == 1)
                {
                    //скачать в темп
                    await DownloadChangedFiles(filesForDownload, tempDir, _updCts.Token);
                }
                else
                {
                    throw new Exception("Должен быть один устаревший файл Updater.exe");
                }

                if (!File.Exists(tempDir + "\\-Updater.exe"))
                {
                    throw new Exception("В папке Temt отсутствует файл Updater.exe");
                }

                // сохранить старую версию
                File.Copy(updaterExePath, buildReservePath + "\\Updater.exe", true);

                //новую версию из темп скопировать в Debug
                File.Copy(tempDir + "\\-Updater.exe", updaterExePath, true);

                // Сохраняем время получения обновлений с сервера
                File.Copy(tempDir + "\\LastUpdatesInfo.txt", @"Engine\Updater\LastUpdatesInfo.txt", true);

                // Сохраняем запись о файлах с новым временем
                File.Copy(tempDir + "\\FilesVersionsTime.txt", @"Engine\Updater\FilesVersionsTime.txt", true);

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string errMsg = $"{OsLocalization.Updater.Message56}: {ex.Message}";
                SaveLogMessage(errMsg);
                return false;
            }
        }

        private async Task DownloadChangedFiles(List<FileState> files, string tempDir, CancellationToken token)
        {
            try
            {
                SaveLogMessage(OsLocalization.Updater.Message46);

                Directory.CreateDirectory(tempDir);

                for (int i = 0; i < files.Count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    string tempFileName = files[i].Name.Replace("\\", "-"); // чтобы в имени файла содержалось указание на подпапки в Debug

                    string tempPath = Path.Combine(tempDir, tempFileName);

                    using (HttpResponseMessage response = await _httpClient.GetAsync(files[i].Url, token))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            SaveLogMessage($"{OsLocalization.Updater.Message47.Split('#')[0]} {files[i].Name} {OsLocalization.Updater.Message47.Split('#')[1]}");
                            continue;
                        }

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(token))

                        using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            contentStream.CopyTo(fileStream);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw;
            }
        }

        private async Task CopyDirectory(string sourceDirName, string destDirName, CancellationToken token)
        {
            try
            {
                // Создаем целевую директорию, если её ещё нет
                Directory.CreateDirectory(destDirName);

                string[] files = Directory.GetFiles(sourceDirName);

                for (int i = 0; i < files.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    if (files[i].Contains("Updater")) //пропускаем Updater.exe
                        continue;

                    string file = files[i];
                    var targetFile = Path.Combine(destDirName, Path.GetFileName(file));
                    File.Copy(file, targetFile, true);
                }

                string[] folders = Directory.GetDirectories(sourceDirName);

                for (int i = 0; i < folders.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    if (folders[i].Contains("\\Engine") || folders[i].Contains("\\Data")) // пропускаем папки, которые не обновляются на Гитхабе
                        continue;

                    string dir = folders[i];
                    var targetSubdir = Path.Combine(destDirName, Path.GetFileName(dir));
                    await CopyDirectory(dir, targetSubdir, token); // Рекурсивно копируем вложенные каталоги
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw;
            }
        }

        private void WriteFilesVersionsTime(string tempDir)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < _serverResp.Files.Count; i++)
                {
                    var fileInfo = _serverResp.Files[i];

                    sb.AppendLine(fileInfo.Name + "#" + fileInfo.LastUpdate + "#" + fileInfo.Size);
                }

                File.WriteAllText(Path.Combine(tempDir, "FilesVersionsTime.txt"), sb.ToString());

            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message48}: {ex.Message}");
            }
        }


        private void StartUpdateApp(string tempDirWithNewFiles)
        {
            try
            {
                string currentExePath = "";

                using (Process process = Process.GetCurrentProcess())
                {
                    try
                    {
                        currentExePath = process.MainModule.FileName;
                    }
                    catch
                    {
                        currentExePath = Environment.ProcessPath;
                    }
                }

                string currentDir = Path.GetDirectoryName(currentExePath);
                string updaterExePath = Path.Combine(currentDir, "Updater.exe");

                // Проверяем наличие обновления
                if (!Directory.Exists(tempDirWithNewFiles))
                {
                    SaveLogMessage(OsLocalization.Updater.Message49);
                    return;
                }

                // разблокировка Updater.exe после скачивания архива
                UnblockFile(updaterExePath);

                try
                {
                    // Запускаем Updater
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = updaterExePath,
                        Arguments = $"\"update\" \"{currentDir}\" \"{tempDirWithNewFiles}\" {Environment.ProcessId} \"{Path.GetFileName(currentExePath)}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    };

                    Process.Start(startInfo);

                    // Закрываем OsEngine
                    Application.Current.Shutdown();

                }
                catch (Exception innerEx)
                {
                    SaveLogMessage($"{OsLocalization.Updater.Message50}: {innerEx.Message}");

                    // Запасной вариант: запуск через cmd
                    ProcessStartInfo cmdStartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"\"{updaterExePath}\" \"update\" \"{currentDir}\" \"{tempDirWithNewFiles}\" {Environment.ProcessId} \"{Path.GetFileName(currentExePath)}\"\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        Verb = "runas"
                    };

                    Process.Start(cmdStartInfo);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message51}: {ex.Message}");
            }
        }

        private void UnblockFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Удаляем поток Zone.Identifier (MOTW)
                    string zoneIdentifierPath = filePath + ":Zone.Identifier";
                    if (File.Exists(zoneIdentifierPath))
                    {
                        File.Delete(zoneIdentifierPath);
                        SaveLogMessage($"{OsLocalization.Updater.Message52}: {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                SaveLogMessage($"{OsLocalization.Updater.Message53} {filePath}: {ex.Message}");
            }
        }

        #endregion

        #region Log
        private void SaveLogMessage(string message)
        {
            try
            {
                var logEntry = new LogEntry()
                {
                    Time = DateTime.Now,
                    Message = message
                };

                if (LogsDataGrid.Dispatcher.CheckAccess())
                {
                    LogsDataGrid.Items.Add(logEntry);
                }
                else
                {
                    LogsDataGrid.Dispatcher.Invoke(() =>
                    {
                        LogsDataGrid.Items.Add(logEntry);
                    });
                }

                string fullMsg = $"{DateTime.Now:G}: {message}\n";

                File.AppendAllText($"Engine\\Log\\UpdaterLog_{DateTime.Now:dd-MM-yyyy}.txt", fullMsg);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{OsLocalization.Updater.Message54}: {ex.Message}");
            }
        }
    }

    public class LogEntry
    {
        public DateTime Time { get; set; }
        public string Message { get; set; }
    }
    #endregion

    public enum UpdaterStatus
    {
        Available,
        Disconnected,
        NoNeed
    }
}
