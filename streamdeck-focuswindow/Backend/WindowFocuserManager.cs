using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Synkrono.FocusWindow.Util;
using Synkrono.FocusWindow.Wrappers;
using FontAwesome.Sharp;
using WinTools.IconExtraction;
using System.Text.RegularExpressions;

namespace Synkrono.FocusWindow.Backend
{
    internal class WindowFocuserManager : IUIHandler
    {
        #region Private Members
        private readonly KeyCoordinates EXIT_KEY_LOCATION = new KeyCoordinates() { Row = 0, Column = 0 };
        private readonly KeyCoordinates NEXT_KEY_LOCATION = new KeyCoordinates() { Row = 1, Column = 0 };
        private readonly KeyCoordinates PREV_KEY_LOCATION = new KeyCoordinates() { Row = 2, Column = 0 };
        private const int APP_KEY_ROW = 0;
        private const int ACTION_KEY_COLUMN_START = 1;


        private static WindowFocuserManager instance = null;
        private static readonly object objLock = new object();

        private ISDConnection connection = null;
        private StreamDeckDeviceInfo streamDeckDeviceInfo = null;
        private int currentPage = 0;
        private int appsPerPage = 0;
        private List<Process> processes = null;
        private Process focusedProcess = null;
        private PluginSettings pluginSettings;
        private string[] filteredApps = null;
        private readonly System.Timers.Timer tmrRefreshVolume;

        private const int NEXT_IMAGE = 0;
        private const int PREV_IMAGE = 1;
        private const int EXIT_IMAGE = 4;
        private readonly string[] imageFiles = { @"images\page_next.png", @"images\page_previous.png", @"images\volume_decrease.png", @"images\volume_increase.png", @"images\exit.png" };
        private readonly Image[] prefectchedImages = null;

        #endregion

        #region Constructors

        public static WindowFocuserManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new WindowFocuserManager();
                    }
                    return instance;
                }
            }
        }

        private WindowFocuserManager()
        {
            try
            {
                tmrRefreshVolume = new System.Timers.Timer
                {
                    Interval = 3000
                };
                tmrRefreshVolume.Elapsed += TmrRefreshVolume_Elapsed;

                // Prefetch images
                prefectchedImages = new Image[imageFiles.Length];
                for (int currIndex = 0; currIndex < imageFiles.Length; currIndex++)
                {
                    if (!File.Exists(imageFiles[currIndex]))
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"WindowFocuserManager: Prefetch image does not exist: {imageFiles[currIndex]}");
                        continue;
                    }

                    prefectchedImages[currIndex] = Image.FromFile(imageFiles[currIndex]);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"WindowFocuserManager constructor exception: {ex}");
            }
        }

        #endregion

        private Task HandleAppRowChange()
        {
            return Task.Run(async () =>
            {
                await Task.Delay(100);
                await FetchApplications(this.filteredApps);
                DrawAppsRow();
            });
        }

        private void TmrRefreshVolume_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            HandleAppRowChange();
        }

        private async Task FetchApplications(string[] filteredApps)
        {
            var processFinder = new ProcessFinder();
            processes = processFinder.GetProcessesWithMainWindow();

            if (filteredApps != null)
            {
                processes = processes.Where(currentProcess => currentProcess.MainWindowTitle != "" && filteredApps.All(filteredEntry => currentProcess.ProcessName != filteredEntry)).ToList();

            }
        }

        private Image FetchFileImage(string fileName)
        {
            Image fileImage = null;
            try
            {
                if (String.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                // Try to extract Icon
                if (!String.IsNullOrEmpty(fileName) && File.Exists(fileName))
                {
                    FileInfo fileInfo = new FileInfo(fileName);
                    var fileIcon = Shell.OfPath(fileInfo.FullName, small: false);
                    if (fileIcon != null)
                    {
                        using (Bitmap fileIconAsBitmap = fileIcon.ToBitmap())
                        {
                            //Logger.Instance.LogMessage(TracingLevel.INFO, $"Bitmap size is: {fileIconAsBitmap.Width}x{fileIconAsBitmap.Height}");
                            fileImage = Tools.GenerateGenericKeyImage(out Graphics graphics);

                            // Check if app icon is smaller than the Stream Deck key
                            if (fileIconAsBitmap.Width < fileImage.Width && fileIconAsBitmap.Height < fileImage.Height)
                            {
                                float position = Math.Min(fileIconAsBitmap.Width / 2, fileIconAsBitmap.Height / 2);
                                graphics.DrawImage(fileIconAsBitmap, position, position, fileImage.Width - position * 2, fileImage.Height - position * 2);
                            }
                            else // App icon is bigger or equals to the size of a stream deck key
                            {
                                graphics.DrawImage(fileIconAsBitmap, 0, 0, fileImage.Width, fileImage.Height);
                            }
                            graphics.Dispose();
                        }
                        fileIcon.Dispose();
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchFileImage exception for {fileName}: {ex}");
            }
            return fileImage;
        }

        private Image FetchProcessImage(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (pluginSettings.ShowPid) return null;
                if (process == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to fetch image for PID: {pid} - process does not exist");
                    return null;
                }
                if (process != null)
                {
                    return FetchFileImage(process?.MainModule?.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"FetchProcessImage exception for PID: {pid} {ex}");
            }
            return null;
        }

        private void InitializeKeys()
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"InitializeKeys");
            UIManager.Instance.ClearAllKeys();
        }

        private void DrawExitKey()
        {
            var action = new UIActionSettings()
            {
                Coordinates = EXIT_KEY_LOCATION,
                Action = UIActions.DrawImage,
                Image = prefectchedImages[EXIT_IMAGE],
                BackgroundColor = Color.Black
            };
            UIManager.Instance.SendUIAction(action);
        }

        private void GenerateMixer()
        {
            InitializeKeys();
            DrawExitKey();
            DrawAppsRow();
        }

        private string SetKeyTitle(Process process, bool hasImage)
        {
            StringBuilder title = new StringBuilder();

            if (pluginSettings.ShowPid)
            {
                title.Append($"{Regex.Replace(process.ProcessName, ".{8}", "$0\n")}\n");

            }

            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"Settings {pluginSettings} ShowName {pluginSettings.ShowName}");

            if (!hasImage || pluginSettings.ShowName)
            {
                if (process != null)
                {
                    // Add new line if mute icon is already shown
                    //title.Append(audioApplication.Name);
                    title.Append(process.MainWindowTitle);
                }
            }

            return title.ToString();
        }

        // WIP: Focus app on long press
        private void GenerateFocused()
        {
            InitializeKeys();
            DrawExitKey();
            DrawFocusedAppOptions();
        }

        private void DrawFocusedAppOptions()
        {
            List<UIActionSettings> actions = new List<UIActionSettings>();


            Image image = FetchProcessImage(focusedProcess.Id);

            actions.Add(new UIActionSettings()
            {
                Coordinates = new KeyCoordinates() { Row = 1, Column = 1 },
                Action = UIActions.DrawImage,
                Title = SetKeyTitle(focusedProcess, image != null),
                Image = image,
                BackgroundColor = Color.Black
            }); ;

            UIManager.Instance.SendUIActions(actions.ToArray());
        }

        private void DrawAppsRow()
        {
            // Draw the relevant list of apps, based on which page we are on
            List<UIActionSettings> actions = new List<UIActionSettings>();
            int startingApp = currentPage * appsPerPage;
            int endingApp = Math.Min(startingApp + appsPerPage, processes.Count);
            int currentColumn = ACTION_KEY_COLUMN_START;
            int currentRow = APP_KEY_ROW;

            // Create actions to show the name of the current list of apps
            for (int currentApp = startingApp; currentApp < endingApp; currentApp++)
            {

                Image image = FetchProcessImage(processes[currentApp].Id);
                Logger.Instance.LogMessage(TracingLevel.DEBUG, $"DrawAppsRow: row {currentRow} col {currentColumn} title {processes[currentApp].MainWindowTitle} index {currentApp}");
                   
                actions.Add(new UIActionSettings()
                {
                    Coordinates = new KeyCoordinates() { Row = currentRow, Column = currentColumn },
                    Action = UIActions.DrawImage,
                    Title = SetKeyTitle(processes[currentApp], image != null),
                    Image = image,
                    BackgroundColor = Color.Black
                }); ;

                currentColumn++;
                if (currentColumn == streamDeckDeviceInfo.Size.Cols)
                {
                    currentRow++;
                    currentColumn = 1;
                }
            }
            UIManager.Instance.SendUIActions(actions.ToArray());

            // Determine if a next / prev key is needed
            if (startingApp > 0)
            {
                DrawPrevKey();
            }
            if ((currentPage + 1) * appsPerPage < processes.Count) // Are there more apps than the ones we are currently showing
            {
                DrawNextKey();
            }

            // Draw Plus and Minus rows based on how many apps are shown
            //DrawPlusRow(endingApp - startingApp + 1);
            //DrawMinusRow(endingApp - startingApp + 1);
        }
        private void DrawPrevKey()
        {
            var action = new UIActionSettings() { Coordinates = PREV_KEY_LOCATION, Action = UIActions.DrawImage, Image = prefectchedImages[PREV_IMAGE], BackgroundColor = Color.Black };
            UIManager.Instance.SendUIAction(action);
        }
        private void DrawNextKey()
        {
            var action = new UIActionSettings() { Coordinates = NEXT_KEY_LOCATION, Action = UIActions.DrawImage, Image = prefectchedImages[NEXT_IMAGE], BackgroundColor = Color.Black };
            UIManager.Instance.SendUIAction(action);
        }



        public async void ProcessLongKeyPressed(KeyCoordinates coordinates)
        {
            if (coordinates.IsCoordinatesSame(EXIT_KEY_LOCATION))
            {
                focusedProcess = null;
                return;
            }

            Process process = getProcessFromCoordinates(coordinates);
            this.focusedProcess = process;
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"Long press {coordinates} process {process.ProcessName}");
            GenerateFocused();

        }

        public async void ProcessKeyPressed(KeyCoordinates coordinates)
        {
            // Exit button pressed
            if (coordinates.IsCoordinatesSame(EXIT_KEY_LOCATION))
            {
                tmrRefreshVolume.Stop();
                await connection.SwitchProfileAsync(null);
                return;
            }

            // Next Button pressed
            if (coordinates.IsCoordinatesSame(NEXT_KEY_LOCATION))
            {
                if ((currentPage + 1) * appsPerPage < processes.Count) // Are there more apps than the ones we are currently showing
                {
                    currentPage++;
                    GenerateMixer();
                }
                return;
            }

            // Prev Button pressed
            if (coordinates.IsCoordinatesSame(PREV_KEY_LOCATION))
            {
                currentPage--;
                if (currentPage < 0)
                {
                    currentPage = 0;
                }
                GenerateMixer();
                return;
            }

            // App button pressed
            if (coordinates.Column >= 1)
            {
                await HandleAppPress(coordinates);
                await HandleAppRowChange();
            }

            // App button pressed (mute/unmute)
            if (coordinates.Row == APP_KEY_ROW)
            {
                //await HandleMuteChange(coordinates);
                await HandleAppRowChange();
            }

        }

        private Process getProcessFromCoordinates(KeyCoordinates coordinates)
        {
            int appIndex = (currentPage * appsPerPage) + (coordinates.Row * 4) + coordinates.Column - 1;
            if (appIndex < processes.Count)
            {
                Process process = processes[appIndex];
                return process;
            }
            return null;
        }

        private async Task HandleAppPress(KeyCoordinates coordinates)
        {
            Process process = getProcessFromCoordinates(coordinates);
            if(process == null)
            {
                return;
            }
            var windowfinder = new WindowFinder();
            (IntPtr main, IntPtr child) = windowfinder.FindWindowWithText(null, process.ProcessName);
            var windowfocuser = new WindowFocuser();
            windowfocuser.SetFocus(main, child, pluginSettings.RestoreWindow);
        }

        public async Task<bool> ShowFocuser(ISDConnection connection, PluginSettings pluginSettings)
        {
            this.connection = connection;
            this.pluginSettings = pluginSettings;
            if (this.pluginSettings.FilteredApps != null)
            {
                this.filteredApps = this.pluginSettings.FilteredApps?.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
                Logger.Instance.LogMessage(TracingLevel.DEBUG, $"filtered {this.filteredApps.Length} settings {pluginSettings.FilteredApps}");
            }
            if (connection == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"WindowMixerManager ShowMixer called with null connection");
                return false;
            }

            streamDeckDeviceInfo = connection.DeviceInfo();
            int keys = streamDeckDeviceInfo.Size.Cols * streamDeckDeviceInfo.Size.Rows;
            currentPage = 0;
            appsPerPage = keys - 3;
            if (!UIManager.Instance.RegisterUIHandler(this, keys))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"WindowMixerManager RegisterGameHandler failed");
                return false;
            }

            await FetchApplications(this.filteredApps);
            //await FetchAudioApplications(mixerSettings.FilteredApps);

            // Wait until the GameUI Action keys have subscribed to get events
            int retries = 0;
            while (!UIManager.Instance.IsUIReady && retries < 100)
            {
                Thread.Sleep(100);
                retries++;
            }
            if (!UIManager.Instance.IsUIReady)
            {
                return false;
            }

            // Generate game board
            GenerateMixer();
            tmrRefreshVolume.Start();

            return true;
        }
    }
}
