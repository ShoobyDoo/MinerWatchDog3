using ModernMinerWatchDog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GetFirstCharIndexFromLine = System.Windows.Forms;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ComboBox = System.Windows.Controls.ComboBox;

namespace ModernMinerWatchDog
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _settingsTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _minerUpdater = new System.Windows.Forms.Timer();
        private NotifyIcon icoTrayIcon = new NotifyIcon();
        private System.Windows.Forms.ContextMenu icoTrayContextMenu;
        private System.Windows.Forms.MenuItem icoTrayMenuItemOpen;
        private System.Windows.Forms.MenuItem icoTrayMenuItemExit;
        private Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Images/miner-icon.ico")).Stream;
        private OpenFileDialog ofdMinerPath = new OpenFileDialog();
        private Process miner = new Process();

        private int UpdateInterval = Properties.Settings.Default.UpdateInterval;
        private int MaxInvalidShares = Properties.Settings.Default.MaxInvalidShares;
        private string latestWatchdogRelease = "";
        private string latestWatchdogChangelog = ""; 

        public bool MinerRunning { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool ConfigImported { get; set; }
        public string PreviousVersion { get; set; }
        public int InvalidShares { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Closing += onExit;

            _timer.Interval = 500;
            _timer.Tick += refreshMinerArgs;
            _timer.Enabled = true;

            _minerUpdater.Interval = UpdateInterval; // 43200000; // every 12 hours
            _minerUpdater.Tick += delegate { checkUpdates(); };
            _minerUpdater.Enabled = true;

            _settingsTimer.Interval = 2000;
            _settingsTimer.Tick += delegate { if (btnSaveSettings.Content.ToString() == "Saved!") { btnSaveSettings.Content = "Save"; } };
            _settingsTimer.Enabled = true;

            icoTrayIcon.Icon = new System.Drawing.Icon(iconStream);
            icoTrayIcon.Text = "Miner WatchDog 3";
            icoTrayIcon.Visible = true;
            icoTrayIcon.MouseClick += icoTrayIcon_MouseClick;

            icoTrayContextMenu = new System.Windows.Forms.ContextMenu();
            icoTrayMenuItemOpen = new System.Windows.Forms.MenuItem();
            icoTrayMenuItemExit = new System.Windows.Forms.MenuItem();

            // Initialize context menu
            icoTrayContextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { icoTrayMenuItemOpen, icoTrayMenuItemExit });

            // Option 1
            icoTrayMenuItemOpen.Index = 0;
            icoTrayMenuItemOpen.Text = "S&how";
            icoTrayMenuItemOpen.Click += IcoTrayMenuItemOpen_Click;

            // Option 2
            icoTrayMenuItemExit.Index = 1;
            icoTrayMenuItemExit.Text = "E&xit";
            icoTrayMenuItemExit.Click += IcoTrayMenuItemExit_Click;

            // Initialize menu on right click
            icoTrayIcon.ContextMenu = icoTrayContextMenu;

            // Main miner process info
            miner.StartInfo.RedirectStandardOutput = true;
            miner.StartInfo.UseShellExecute = false;
            miner.StartInfo.CreateNoWindow = true;

            hkGaming.HotkeyTextBox.TextChanged += hkGaming_TextChanged;
            hkMining.HotkeyTextBox.TextChanged += hkMining_TextChanged;
        }

        private void IcoTrayMenuItemExit_Click(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void IcoTrayMenuItemOpen_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Focus();
            this.Activate();
        }

        private void onExit(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
            icoTrayIcon.Icon.Dispose();
            icoTrayIcon.Dispose();
            icoTrayIcon = null;
            terminateMiner();
            terminateWatchDog();
            try
            {
                miner.Kill();
            }
            catch (Exception)
            {
                Helpers.DebugConsole(txtDebug, "Could not kill the miner executable. Perhaps no miner process running/found?", "Exit");
            }
            Helpers.DebugConsole(txtDebug, "\n\n-------------------\nAPP INSTANCE EXITED\n\n", "APP_END");
        }

        void refreshMinerArgs(object sender, EventArgs e)
        {
            string pool = Properties.Settings.Default.MinerPool;
            string wallet = Properties.Settings.Default.MinerWallet;
            string worker = Properties.Settings.Default.MinerWorker;
            string pass = Properties.Settings.Default.MinerPassword;
            string coin = Properties.Settings.Default.MinerCoin;
            string addArgs = Properties.Settings.Default.MinerAdditionalArgs;

            rtbMinerArgs.Text = String.Format("-pool {0} -wal {1} -worker {2} -pass {3} -coin {4} {5}", pool, wallet.Trim(), worker.Trim(), pass, coin, addArgs);
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void TopBar_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Application.Current.MainWindow.WindowState != WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            }
        }

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;

            if (Properties.Settings.Default.MinimizeToTray == true)
            {
                if (WindowState.Minimized == this.WindowState)
                {
                    Helpers.DebugConsole(txtDebug, "Program has been sent to tray", "Settings");

                    icoTrayIcon.BalloonTipText = "Monitoring running processes in the background...";
                    icoTrayIcon.BalloonTipTitle = "Miner WatchDog 3";
                    icoTrayIcon.BalloonTipIcon = ToolTipIcon.Info; 
                    icoTrayIcon.ShowBalloonTip(5);

                    this.Hide();
                }
            }
        }

        private void ButtonMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow.WindowState != WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            }
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        
        void refreshProcessList()
        {
            string processListMsg = "";
            string minerStatus = "ERROR! - Cannot access your miner (Is this first launch?) Go to Settings!";
            bool gamesRunning = false;
            bool gamesAllowed = false;
            bool minerNotFound = false;
            bool parentFound = false;

            while (true)
            {
                Thread tProcMiner = new Thread(refreshMinerOutput);
                tProcMiner.IsBackground = true;

                double REFRESH_INTERVAL = 15.0;
                miner.StartInfo.FileName = Properties.Settings.Default.MinerPath;
                miner.StartInfo.Arguments = Properties.Settings.Default.MinerArgs.ToString();

                List<string> gamesList = Properties.Settings.Default.Prevented.Cast<string>().ToList(); // Prevented
                List<string> gamesIgnored = Properties.Settings.Default.Allowed.Cast<string>().ToList(); // Allowed

                string processList = "";
                string allProcesses = "";
                string processStatus = "";

                // Search for processes that are running. (Filter games, programs, etc.)
                Process[] processCollection = Process.GetProcesses();

                foreach (Process p in processCollection)
                {

                    if (gamesList.Contains(p.ProcessName))
                    {
                        processStatus = String.Format("A GPU intensive program [{0}] is currently running...", p.ProcessName);
                        processList += p.Id.ToString().PadRight(20) + p.ProcessName + "\n";
                        gamesAllowed = false;
                        break;
                    }
                    else if (gamesIgnored.Contains(p.ProcessName))
                    {
                        processStatus = String.Format("A Non-GPU intensive program [{0}] is currently running...", p.ProcessName);
                        processList += p.Id.ToString().PadRight(20) + p.ProcessName + "\n";
                        gamesAllowed = true;
                        parentFound = true;
                    }

                    if (!parentFound)
                    {
                        processStatus = "No blacklisted program(s) currently running...";
                        allProcesses += p.Id.ToString().PadRight(20) + p.ProcessName + "\n";
                        gamesAllowed = false;
                        parentFound = false;
                    }
                }

                if (processList.Length < 1)
                {
                    processList = allProcesses;
                    processListMsg = "See below for ALL running processes in case you would like to add one in the settings menu.\n---\n\n";
                    parentFound = false;
                    gamesRunning = false;
                }
                else
                {
                    processListMsg = "";
                    gamesRunning = true;
                }

                if (gamesAllowed) { gamesRunning = false; }

                if (gamesRunning)
                {
                    if (MinerRunning)
                    {
                        miner.Kill();
                        MinerRunning = false;
                        minerStatus = "Miner is currently: OFFLINE 🔴";

                        this.Dispatcher.Invoke(() => { Helpers.DebugConsole(txtDebug, "Experimental: Attempting hot-key based clock preset for non-miner related task..."); });

                        string presetMiner = switchMinerProfile(Properties.Settings.Default.HotkeyGaming);

                        this.Dispatcher.Invoke(() =>
                        {
                            TextRange document = new TextRange(
                                // TextPointer to the start of content in the RichTextBox.
                                txtConsole.Document.ContentEnd,
                                // TextPointer to the end of content in the RichTextBox.
                                txtConsole.Document.ContentEnd
                            );
                            //TextRange document = new TextRange(txtConsole.Document.ContentStart, txtConsole.Document.ContentEnd);
                            document.Text += "\nMinerWatchDog: Miner has been automatically stopped due to a gpu-intensive program\nMinerWatchDog: " + presetMiner + "\n";
                            document.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                            txtConsole.ScrollToEnd();

                            Helpers.DebugConsole(txtDebug, "Miner process killed (" + Properties.Settings.Default.MinerPath + ")", "Main");
                            
                            icoTrayIcon.BalloonTipText = "Miner stopped: Blacklisted program(s) running.";
                            icoTrayIcon.BalloonTipTitle = "Miner WatchDog 3";
                            icoTrayIcon.BalloonTipIcon = ToolTipIcon.Info;
                            icoTrayIcon.ShowBalloonTip(5);

                            Helpers.DebugConsole(txtDebug, "Miner stopped balloon notification sent.", "Main");
                        });
                        tProcMiner.Abort(); // change
                    }
                    else
                    {
                        minerStatus = "Miner is currently: OFFLINE 🔴";
                    }
                }
                else
                {
                    if (MinerRunning == false)
                    {
                        try
                        {
                            miner.Start();
                            minerNotFound = false;
                            MinerRunning = true;
                            minerStatus = "Miner is currently: ONLINE 🔴";

                            this.Dispatcher.Invoke(() => 
                            {
                                Helpers.DebugConsole(txtDebug, "Miner process started (" + Properties.Settings.Default.MinerPath + ")", "Main");

                                icoTrayIcon.BalloonTipText = "Miner started: Blacklisted program(s) are no longer running.";
                                icoTrayIcon.BalloonTipTitle = "Miner WatchDog 3";
                                icoTrayIcon.BalloonTipIcon = ToolTipIcon.Info;
                                icoTrayIcon.ShowBalloonTip(5);

                                Helpers.DebugConsole(txtDebug, "Miner started balloon notification sent.", "Main");
                            });
                        }
                        catch (Exception)
                        {
                            minerNotFound = true;
                        }

                        if (!minerNotFound)
                        {
                            tProcMiner.Start();
                        }
                    }
                }

                this.Dispatcher.Invoke(() =>
                {
                    TextRange rangeStatus = new TextRange(txtStatus.Document.ContentStart, txtStatus.Document.ContentEnd);
                    rangeStatus.Text = processStatus.ToString();

                    TextRange rangeMinerStatus = new TextRange(txtMinerStatus.Document.ContentStart, txtMinerStatus.Document.ContentEnd);
                    rangeMinerStatus.Text = minerStatus.ToString();

                    RichTextManipulation.FromTextPointer(txtMinerStatus.Document.ContentStart, txtMinerStatus.Document.ContentEnd, "ONLINE 🔴", Brushes.LimeGreen);
                    RichTextManipulation.FromTextPointer(txtMinerStatus.Document.ContentStart, txtMinerStatus.Document.ContentEnd, "OFFLINE 🔴", Brushes.Salmon);

                    txtProcesses.Text = processListMsg + "Process Id".PadRight(20) +
                                        "Process Name\n----------          ------------\n" +
                                        processList +
                                        "----------          ------------";
                });

                Thread.Sleep((int)(REFRESH_INTERVAL * 1000));
            }
        }

        public string switchMinerProfile(string hotkey)
        {
            var line = "";

            this.Dispatcher.Invoke(() => { Helpers.DebugConsole(txtDebug, "Attempting to swap hotkey presets...", "Hotkey"); });

            try
            {
                string presetControllerPath = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
                presetControllerPath = System.IO.Path.GetDirectoryName(presetControllerPath);
                presetControllerPath = presetControllerPath.Replace(@"file:\", "") + @"\Core\preset-controller.exe";

                var presetChanger = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = presetControllerPath,
                        Arguments = String.Format("\"{0}\"", hotkey),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                presetChanger.Start();

                while (!presetChanger.StandardOutput.EndOfStream)
                {
                    line += presetChanger.StandardOutput.ReadLine() + "... ";
                }

                presetChanger.WaitForExit();

            }
            catch (Exception ex)
            {
                Helpers.DebugConsole(txtDebug, "An error occurred while attempting to switch presets! Stacktrace: " + ex.GetBaseException(), level: 2);
            }

            return line.Trim();
        }

        void refreshMinerOutput()
        {
            while (!miner.StandardOutput.EndOfStream)
            {
                this.Dispatcher.Invoke(() =>
                {
                    TextRange document = new TextRange(
                        // TextPointer to the start of content in the RichTextBox.
                        txtConsole.Document.ContentStart,
                        // TextPointer to the end of content in the RichTextBox.
                        txtConsole.Document.ContentEnd
                    );

                    TextRange minerOutputLine = new TextRange(
                        // TextPointer to the start of content in the RichTextBox.
                        txtConsole.Document.ContentEnd,
                        // TextPointer to the end of content in the RichTextBox.
                        txtConsole.Document.ContentEnd
                    );

                    minerOutputLine.Text = miner.StandardOutput.ReadLine() + Environment.NewLine;

                    if (minerOutputLine.Text.StartsWith("Eth speed:") || minerOutputLine.Text.StartsWith("Eth: Connecting"))
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Cyan);
                    }
                    else if (minerOutputLine.Text.StartsWith("GPU1:") || minerOutputLine.Text.StartsWith("GPUs power:"))
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Orchid);
                        
                        if (minerOutputLine.Text.Contains("DAG generated"))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                Helpers.DebugConsole(txtDebug, "Experimental: Attempting hot-key based clock preset for non-miner related task...");
                                string presetMiner = switchMinerProfile(Properties.Settings.Default.HotkeyMining);
                                TextRange documentEndLine = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);

                                documentEndLine.Text += "MinerWatchDog: " + presetMiner + "\n";
                                documentEndLine.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                            });
                        }
                    }
                    else if (minerOutputLine.Text.StartsWith("Eth: GPU1:") || minerOutputLine.Text.StartsWith("Eth: Share") || minerOutputLine.Text.StartsWith("Eth: Connected"))
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Lime);
                    }
                    else if (
                        minerOutputLine.Text.StartsWith("*") || 
                        minerOutputLine.Text.StartsWith("Eth: Mining") || 
                        minerOutputLine.Text.StartsWith("Eth: DAG epoch") ||
                        minerOutputLine.Text.StartsWith("Eth: Accepted") || 
                        minerOutputLine.Text.StartsWith("Eth: Incorrect shares") || 
                        minerOutputLine.Text.StartsWith("Eth: Maximum") || 
                        minerOutputLine.Text.StartsWith("Eth: Average") || 
                        minerOutputLine.Text.StartsWith("Eth: Effective"))
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Yellow);
                    }
                    else if (minerOutputLine.Text.StartsWith("Eth: Incorrect"))
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Red);
                        InvalidShares += 1;

                        if (InvalidShares > MaxInvalidShares)
                        {
                            TextRange documentEndLine = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);

                            documentEndLine.Text += "MinerWatchDog: A high number of invalid shares (" + InvalidShares + ") were detected...\n";
                            documentEndLine.Text += "MinerWatchDog: Attempting to restart miner automatically...\n";
                            documentEndLine.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));

                            miner.Kill();
                            miner.Start();

                            InvalidShares = 0;
                        }
                    }
                    else
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.LightGray);
                    }

                    if (document.Text.Length > 10000)
                    {
                        txtConsole.Document.Blocks.Clear();
                        document.Text = "" + Environment.NewLine;
                        txtConsole.AppendText("MinerWatchDog: Console output was cleared at 10,000 characters to save memory\n");
                        document.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                    }

                    //txtConsole.SelectionStart = txtConsole.SelectAll();
                    txtConsole.ScrollToEnd();

                    //txtDebug.SelectionStart = txtDebug.Text.Length;
                    //txtDebug.ScrollToEnd();
                });
            }
        }

        void terminateMiner()
        {
            Process[] processCollection = Process.GetProcesses();
            string minerPath = Properties.Settings.Default.MinerPath;

            string[] pathSplit = minerPath.Split('\\');
            string minerExec = pathSplit[pathSplit.Length - 1];
            string firstFive = "Phoen"; // TODO: FALLBACK TO PHOE
            int minerInstances = 0;

            try
            {
                firstFive = minerExec.Substring(0, 5);
            }
            catch (ArgumentOutOfRangeException)
            {
                Helpers.DebugConsole(txtDebug, "first five of miner substring extraction is out of range. Likely no miner path provided, as default=C:\\ or null", "Main");
            }

            foreach (Process p in processCollection)
            {
                if (p.ProcessName.StartsWith(firstFive))
                {
                    try
                    {
                        minerInstances++;
                        Helpers.DebugConsole(txtDebug, String.Format("{0} running instances: {1}", minerExec, minerInstances), "Main");
                        p.Kill();
                        Helpers.DebugConsole(txtDebug, "Killed running miner instance: " + p.ProcessName, "Main");
                    }
                    catch (Exception)
                    {
                        Helpers.DebugConsole(txtDebug, "Process object NOT killable, (permissions?)...", "Main");
                    }

                }
            }
        }

        void terminateWatchDog()
        {
            int minerInstances = 0;
            Process[] processCollection = Process.GetProcesses();
            foreach (Process p in processCollection)
            {
                if (p.ProcessName.StartsWith("MinerWatchDog"))
                {
                    try
                    {
                        minerInstances++;
                        Helpers.DebugConsole(txtDebug, "MinerWatchDog3 running instances: " + minerInstances);
                        if (minerInstances > 1)
                        {
                            Helpers.DebugConsole(txtDebug, "Killed running process instance: " + p.ProcessName, "Main");
                            p.Kill();
                            break;
                        }

                    }
                    catch (Exception exc)
                    {
                        Helpers.DebugConsole(txtDebug, String.Format("Error while terminating watchdog: {0}", exc.Message), level: 2);
                    }
                }
            }
        }

        public string getLatestWatchdog(string author, string repo)
        {
            const string GITHUB_API = "https://api.github.com/repos/{0}/{1}/releases";
            WebClient webClient = new WebClient();
            webClient.Headers.Add("User-Agent", "Unity web player");
            Uri uri = new Uri(string.Format(GITHUB_API, author, repo));
            string releaseList = webClient.DownloadString(uri);
            
            return releaseList;
        }

        public string checkMinerUpdate(bool silent = false)
        {
            string silentFlag = "";
            var line = "";

            Helpers.DebugConsole(txtDebug, "Checking to see if miner has updates...", "Updater PM");

            try
            {
                string updaterPath = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
                updaterPath = System.IO.Path.GetDirectoryName(updaterPath);
                updaterPath = updaterPath.Replace(@"file:\", "") + @"\Miner\miner-updater.exe";

                if (silent)
                {
                    silentFlag = "--silent";
                }

                var minerUpdates = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = String.Format("--check {0} phoenixminer", silentFlag),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                minerUpdates.Start();

                while (!minerUpdates.StandardOutput.EndOfStream)
                {
                    line += minerUpdates.StandardOutput.ReadLine();
                    //Helpers.DebugConsole(txtDebug, line, "Changelog");
                }

                minerUpdates.WaitForExit();

            }
            catch (Exception)
            {
                Helpers.DebugConsole(txtDebug, "An error occurred while attempting to check for miner updates!", level: 2);
            }

            return line.Trim();

        }

        public string checkLocalMinerVersion(bool silent = false, string updaterPath = "")
        {
            var line = "";

            //Helpers.DebugConsole(txtDebug, "Checking local miner for version...", "Updater");

            try
            {
                if (updaterPath == string.Empty) { updaterPath = Properties.Settings.Default.MinerPath; }

                var minerVersion = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = "--version phoenixminer",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                minerVersion.Start();

                while (!minerVersion.StandardOutput.EndOfStream)
                {
                    line = minerVersion.StandardOutput.ReadLine();
                    break;
                }

                minerVersion.WaitForExit();

                string[] versionSplit = line.Split(' ');
                string versionExtract = versionSplit[2];

                if (silent)
                {
                    line = versionExtract;
                }

            }
            catch (Win32Exception)
            {
                Helpers.DebugConsole(txtDebug, "Cannot access miner executable, likely not provided?", level: 1);
            }
            catch (Exception ex)
            {
                Helpers.DebugConsole(txtDebug, "General exception handler. Stacktrace:\n" + ex.GetBaseException(), level: 2);
            }

            return line.Trim();
        }

        public string checkLatestChangelog(bool silent = false)
        {
            string silentFlag = "";
            var line = "";

            Helpers.DebugConsole(txtDebug, "Checking for changelog...", "Changelog");

            try
            {
                string updaterPath = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
                updaterPath = System.IO.Path.GetDirectoryName(updaterPath);
                updaterPath = updaterPath.Replace(@"file:\", "") + @"\Miner\miner-updater.exe";

                if (silent)
                {
                    silentFlag = "--silent";
                }

                var minerUpdates = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = "--changelog " + silentFlag + " phoenixminer",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                minerUpdates.Start();

                while (!minerUpdates.StandardOutput.EndOfStream)
                {
                    line += minerUpdates.StandardOutput.ReadLine() + '\n';
                }

                minerUpdates.WaitForExit();

            }
            catch (Exception)
            {
                Helpers.DebugConsole(txtDebug, "An error occurred while attempting to check for miner updates!", level: 2);
            }

            //return line.Trim();
            return line;

        }

        void checkUpdates(bool withBaloon = false)
        {
            JArray jsonRaw = (JArray)JsonConvert.DeserializeObject(getLatestWatchdog("ShoobyDoo", "MinerWatchDog3"));
            latestWatchdogRelease = jsonRaw[0]["tag_name"].ToString();
            latestWatchdogChangelog = jsonRaw[0]["body"].ToString();

            Version localVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Version githubVersion = new Version(latestWatchdogRelease);

            if (githubVersion.CompareTo(localVersion) > 0)
            {
                if (withBaloon)
                {
                    // If i need to activate baloon tip, perhaps for future update functionality.

                    //icoTrayIcon.BalloonTipText = "Update available! See Github for more details...";
                    //icoTrayIcon.BalloonTipTitle = "Miner WatchDog 3";
                    //icoTrayIcon.BalloonTipIcon = ToolTipIcon.Warning;

                    //icoTrayIcon.Visible = true;
                    //icoTrayIcon.ShowBalloonTip(5);

                    //// TODO: something something remove this and replace it in the button about section area
                    //MessageBox.Show(latestWatchdogChangelog + "\n\nSee github for installer.", "Update for Miner Watchdog 3 is available.");
                }

                if (txtUpdateAvailable.Visibility != Visibility.Visible)
                {
                    gridMainViews.RowDefinitions.Add(new RowDefinition());
                    gridMainViews.RowDefinitions[gridMainViews.RowDefinitions.Count - 1].Height = GridLength.Auto;

                    txtUpdateAvailable.Visibility = Visibility.Visible;
                    bdrUpdateAvailable.Visibility = Visibility.Visible;
                }


                // Old MW3 Update available code
                txtVersion.Foreground = Brushes.LightGoldenrodYellow;
                tbTTWdUpdateChangelog.Text = String.Format("Miner WatchDog 3 Update ({0}):\n\n{1}", latestWatchdogRelease, latestWatchdogChangelog);

                txtMinerVersion.Foreground = Brushes.LightGoldenrodYellow;
                txtMinerVersion.TextDecorations = TextDecorations.Underline;

                txtUpdates.Foreground = Brushes.LightGoldenrodYellow;
                txtUpdates.Text = "There's an update available.";

                btnSeeMore.IsEnabled = true;
                Helpers.DebugConsole(txtDebug, "There is an update for Miner WatchDog 3 available!", "Updater WD");

            }
            else
            {
                if (txtUpdateAvailable.Visibility == Visibility.Visible)
                {
                    gridMainViews.RowDefinitions.RemoveAt(gridMainViews.RowDefinitions.Count - 1);

                    txtUpdateAvailable.Visibility = Visibility.Hidden;
                    bdrUpdateAvailable.Visibility = Visibility.Hidden;
                }


                // Old MW3 Update available code
                txtVersion.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString("#EAECEB");
                txtVersion.TextDecorations = null;
                tbTTWdUpdateChangelog.Text = "You have the latest Miner WatchDog 3 version.";

                txtMinerVersion.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString("#EAECEB");
                txtUpdates.Foreground = Brushes.LightSeaGreen;

                txtUpdates.Text = "You're on the latest version.";

                Helpers.DebugConsole(txtDebug, "You have the latest version of Miner WatchDog 3!", "Updater WD");
                btnSeeMore.IsEnabled = false;
            }

            string localMiner = checkLocalMinerVersion(silent: true);
            string latestMiner = checkMinerUpdate(silent: true);

            Helpers.DebugConsole(txtDebug, "Local miner: " + (localMiner == "" ? "None" : localMiner), "Updater PM");
            Helpers.DebugConsole(txtDebug, "Latest miner: " + latestMiner, "Updater PM");

            latestMiner = "6.2f"; 
            if (localMiner.Trim() == latestMiner.Trim())
            {
                tbTTUpdateChangelog.Text = "You have the latest PhoenixMiner version.";
                txtMinerVersion.Text = String.Format("Status: Up to Date!");
                txtMinerVersion.Foreground = Brushes.LightSeaGreen;
                txtMinerVersion.TextDecorations = null;
                txtMinerVersion.Cursor = System.Windows.Input.Cursors.Arrow;
                UpdateAvailable = false;
            }
            else if (localMiner == "")
            {
                tbTTUpdateChangelog.Text = "Can't access your (Miner).exe, set the correct path in the miner settings.";
                txtMinerVersion.Text = String.Format("Status: Error! (No Miner)");
                txtMinerVersion.Foreground = Brushes.IndianRed;
                return;
            }
            else
            {
                string latestChangelog = checkLatestChangelog(silent: true);
                Helpers.DebugConsole(txtDebug, String.Format("Changelog found, Raw: {0} characters.", latestChangelog.Length), "Changelog");

                txtMinerVersion.Text = String.Format("Status: Update available! ({0}) ⧉", latestMiner.Trim());
                txtMinerVersion.Foreground = Brushes.LightGoldenrodYellow;
                txtMinerVersion.TextDecorations = TextDecorations.Underline;
                UpdateAvailable = true;

                tbTTUpdateChangelog.Text = "PhoenixMiner Update:\n\n" + latestChangelog;
            }

            txtLocalMiner.Text = "Miner: PhoenixMiner " + localMiner;
        }

        bool checkConfig()
        {
            Helpers.DebugConsole(txtDebug, "Searching for previous configuration files...", "Config");
            Version _old = new Version("0.0.0.0");

            try
            {
                string[] localAppdata = Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\MinerWatchDog3");
                string allConfigsPath = localAppdata[0];
                string configFilename = "\\user.config";
                string oldConfigPath = "";
                string newConfigPath = "";

                foreach (string version in Directory.GetDirectories(allConfigsPath))
                {
                    Version _current = new Version(version.Split('\\').Last());

                    if (_current.CompareTo(_old) > 0 && _current != Assembly.GetExecutingAssembly().GetName().Version)
                    {
                        _old = _current;
                        oldConfigPath = version;
                    }
                    else if (_current == Assembly.GetExecutingAssembly().GetName().Version && File.Exists(version + configFilename))
                    {
                        newConfigPath = version + configFilename;
                        Helpers.DebugConsole(txtDebug, String.Format("Config for current exists at local: \\{0}{1}", _current, configFilename), "Config");
                    }
                }

                Helpers.DebugConsole(txtDebug, oldConfigPath == "" ? "No previous installations found, new version?" : "Found previous installation, config file at: " + oldConfigPath, "Config");

                if (oldConfigPath != "" && !File.Exists(newConfigPath))
                {
                    Helpers.DebugConsole(txtDebug, "ACTION REQUIRED!");

                    try
                    {
                        newConfigPath = newConfigPath == "" ? newConfigPath = oldConfigPath.Replace(_old.ToString(), Assembly.GetExecutingAssembly().GetName().Version.ToString()) : newConfigPath;
                        Helpers.DebugConsole(txtDebug, "Creating directory at: " + newConfigPath, "Config");
                        Directory.CreateDirectory(newConfigPath);
                        Helpers.DebugConsole(txtDebug, String.Format("Copying {0} -> {1}", oldConfigPath + configFilename, newConfigPath + configFilename), "Config");
                        File.Copy(oldConfigPath + configFilename, newConfigPath + configFilename, true);

                        PreviousVersion = _old.ToString();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Helpers.DebugConsole(txtDebug, "\nStacktrace:\n" + ex.GetBaseException(), "Config", 2);
                        return false;
                    }
                }
                else
                {
                    Helpers.DebugConsole(txtDebug, "Config imports not required // config exists on latest version.", "Config");
                    return false;
                }

            }
            catch (DirectoryNotFoundException ex)
            {
                Helpers.DebugConsole(txtDebug, ex.Message, "Config", 2);
                return false;
            }
            catch (Exception ex)
            {
                Helpers.DebugConsole(txtDebug, "\nStacktrace:\n" + ex.GetBaseException(), "Config", 2);
                return false;
            }
        }

        public string[] listOfSettings()
        {
            txtMinerPath.Text = Properties.Settings.Default.MinerPath;
            rtbMinerArgs.Text = Properties.Settings.Default.MinerArgs;
            txtPool.Text = Properties.Settings.Default.MinerPool;
            txtWallet.Text = Properties.Settings.Default.MinerWallet;
            txtWorker.Text = Properties.Settings.Default.MinerWorker;
            txtPassword.Text = Properties.Settings.Default.MinerPassword;
            txtCoin.Text = Properties.Settings.Default.MinerCoin;
            txtAdditionalArgs.Text = Properties.Settings.Default.MinerAdditionalArgs;

            string[] minerSettings = {
                "Miner Path      -> " + txtMinerPath.Text,
                "Crypto Pool     -> " + txtPool.Text,
                "Crypto Wallet   -> " + txtWallet.Text,
                "Miner Worker    -> " + txtWorker.Text,
                "Pool Password   -> " + txtPassword.Text,
                "Crypto Coin     -> " + txtCoin.Text,
                "Additional Args -> " + txtAdditionalArgs.Text
            };

            return minerSettings;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            string arg = "";
            if (args.Length > 1)
            {
                if (args[1].ToString() == "--debug")
                {
                    Helpers.Debug = true;
                    arg = args[1];
                    Helpers.DebugConsole(txtDebug, "APPLICATION START: (debug mode)!", "APP_START");
                }
            }
            else
            {
                Helpers.Debug = false;
                Helpers.DebugConsole(txtDebug, "APPLICATION START: (release mode)!", "APP_START");
            }

            TextRange document = new TextRange(
                // TextPointer to the start of content in the RichTextBox.
                txtConsole.Document.ContentStart,
                // TextPointer to the end of content in the RichTextBox.
                txtConsole.Document.ContentEnd
            );

            document.Text = String.Format(@"    __  ____                _       __      __       __    ____             _____
   /  |/  (_)___  ___  ____| |     / /___ _/ /______/ /_  / __ \____  ____ |__  /
  / /|_/ / / __ \/ _ \/ ___/ | /| / / __ `/ __/ ___/ __ \/ / / / __ \/ __ `//_ < 
 / /  / / / / / /  __/ /   | |/ |/ / /_/ / /_/ /__/ / / / /_/ / /_/ / /_/ /__/ / 
/_/  /_/_/_/ /_/\___/_/    |__/|__/\__,_/\__/\___/_/ /_/_____/\____/\__, /____/  
                                                                   /____/        " + 
                "\n[{0} by Doomlad 2021-{1} / ({2})]\n\n", 
                Assembly.GetExecutingAssembly().GetName().Version.ToString(), 
                DateTime.Now.Year, 
                Helpers.Debug ? "debug" : "release");

            document.ApplyPropertyValue(ForegroundProperty, Brushes.LightGray);

            UpdateInterval = Properties.Settings.Default.UpdateInterval;
            MaxInvalidShares = Properties.Settings.Default.MaxInvalidShares;

            Helpers.DebugConsole(txtDebug, String.Format("Update interval: {0} ms", UpdateInterval), "Settings");
            Helpers.DebugConsole(txtDebug, String.Format("Maximum invalid shares: {0}", MaxInvalidShares), "Settings");

            lbCheckUpdates.SelectedIndex = Properties.Settings.Default.UpdateIntervalSelectedIndex; // get from prev session
            txtMaxIncorrectShares.Text = MaxInvalidShares.ToString();

            ConfigImported = checkConfig();
            checkUpdates();

            if (Properties.Settings.Default.StartMinimized && Properties.Settings.Default.MinimizeToTray)
            {
                Helpers.DebugConsole(txtDebug, "Program has been sent to tray", "Settings");
                icoTrayIcon.BalloonTipText = "Monitoring running processes in the background...";
                icoTrayIcon.BalloonTipTitle = "Miner WatchDog 3";
                icoTrayIcon.BalloonTipIcon = ToolTipIcon.Info;

                icoTrayIcon.Visible = true;
                icoTrayIcon.ShowBalloonTip(5);

                Hide();
            }

            if (Properties.Settings.Default.StartMinimized) { this.WindowState = WindowState.Minimized; }

            string publishVersion = "0.0.0.0";
            try
            {
                publishVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
            catch (Exception)
            {
                publishVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            finally
            {
                txtVersion.Text = "WatchDog: " + publishVersion;
            }

            terminateWatchDog();
            terminateMiner();

            Thread tProc = new Thread(refreshProcessList);
            tProc.IsBackground = true;
            tProc.Start();

            rbConsole.IsChecked = true;
            rbMiner.IsChecked = true;

            Helpers.DebugConsole(txtDebug, "Retreiving user settings...", "Settings");

            // Moved to function
            string[] minerSettings = listOfSettings();

            foreach (string i in minerSettings) { Helpers.DebugConsole(txtDebug, i, "Settings"); }

            List<string> gamesList = Properties.Settings.Default.Prevented.Cast<string>().ToList(); // Prevented
            foreach (var prevented in gamesList)
            {
                Helpers.DebugConsole(txtDebug, "Prevented -> " + prevented, "Settings");
                rtbPrevented.Text += prevented + "\n";
            }

            List<string> gamesIgnored = Properties.Settings.Default.Allowed.Cast<string>().ToList(); // Allowed
            foreach (var allowed in gamesIgnored)
            {
                Helpers.DebugConsole(txtDebug, "Allowed -> " + allowed, "Settings");
                rtbAllowed.Text += allowed + "\n";
            }

            if (Properties.Settings.Default.MinerPath != "C:/" || Properties.Settings.Default.MinerPath != "")
            {
                txtMinerPath.Text = Properties.Settings.Default.MinerPath;
            }

            if (Properties.Settings.Default.StartMinimized) { btnRunMinimized.IsChecked = true; txtStartMinimized.Text = "Enabled"; }
            if (Properties.Settings.Default.MinimizeToTray) { btnMinimizeToTray.IsChecked = true; txtMinimizeToTray.Text = "Enabled"; }
            if (Properties.Settings.Default.RunAtStartup) { btnRunAtStartup.IsChecked = true; txtSystemStartup.Text = "Enabled"; }

            Helpers.DebugConsole(txtDebug, "Start minimized  -> " + Properties.Settings.Default.StartMinimized, "Settings");
            Helpers.DebugConsole(txtDebug, "Minimize to tray -> " + Properties.Settings.Default.MinimizeToTray, "Settings");
            Helpers.DebugConsole(txtDebug, "Run at startup   -> " + Properties.Settings.Default.RunAtStartup, "Settings");

            if (!String.IsNullOrEmpty(Properties.Settings.Default.HotkeyGaming))
            {
                txtHkGaming.Text = Properties.Settings.Default.HotkeyGaming;
                hkGaming.HotkeyTextBox.Foreground = Brushes.Transparent;
            }

            if (!String.IsNullOrEmpty(Properties.Settings.Default.HotkeyMining))
            {
                txtHkMining.Text = Properties.Settings.Default.HotkeyMining;
                hkMining.HotkeyTextBox.Foreground = Brushes.Transparent;
            }

            Helpers.DebugConsole(txtDebug, "Gaming Hotkey -> " + Properties.Settings.Default.HotkeyGaming, "Settings");
            Helpers.DebugConsole(txtDebug, "Mining Hotkey -> " + Properties.Settings.Default.HotkeyMining, "Settings");

            tbAbout.Text = "This program constantly monitors all running processes for user defined entries and will automatically start / stop crypto mining software based on which entries deny mining.\n\n(Currently only PhoenixMiner is supported.)";

            Helpers.DebugConsole(txtDebug, "Settings loaded successfully, all components fully loaded.", "Settings");

            if (ConfigImported)
            {
                MessageBox.Show(String.Format("Config imported from your previous installation: ({0})", PreviousVersion), "Config Importer");
            }
        }

        #region "// MINER PAGE //"
        private void rbMiner_Checked(object sender, RoutedEventArgs e)
        {
            gridMiner.Visibility = Visibility.Visible;
            gridSettings.Visibility = Visibility.Hidden;
            gridAbout.Visibility = Visibility.Hidden;
        }

        private void rbConsole_Checked(object sender, RoutedEventArgs e)
        {
            gridMinerConsole.Visibility = Visibility.Visible;
            gridMinerProcesses.Visibility = Visibility.Hidden;
            gridMinerDebug.Visibility = Visibility.Hidden;
        }

        private void rbProcesses_Checked(object sender, RoutedEventArgs e)
        {
            gridMinerConsole.Visibility = Visibility.Hidden;
            gridMinerProcesses.Visibility = Visibility.Visible;
            gridMinerDebug.Visibility = Visibility.Hidden;
        }

        private void rbDebug_Checked(object sender, RoutedEventArgs e)
        {
            gridMinerConsole.Visibility = Visibility.Hidden;
            gridMinerProcesses.Visibility = Visibility.Hidden;
            gridMinerDebug.Visibility = Visibility.Visible;
        }
        #endregion

        // SETTINGS PAGE //
        private void rbSettings_Checked(object sender, RoutedEventArgs e)
        {
            rbSettingsGeneral.IsChecked = true;
            gridMiner.Visibility = Visibility.Hidden;
            gridSettings.Visibility = Visibility.Visible;
            gridAbout.Visibility = Visibility.Hidden;
        }

        private void rbSettingsGeneral_Checked(object sender, RoutedEventArgs e)
        {
            gridSettingsGeneral.Visibility = Visibility.Visible;
            gridSettingsMinerConfig.Visibility = Visibility.Hidden;
            gridSettingsSystem.Visibility = Visibility.Hidden;
            gridSettingsGPU.Visibility = Visibility.Hidden;
        }

        private void rbSettingsMiner_Checked(object sender, RoutedEventArgs e)
        {
            gridSettingsSystem.Visibility = Visibility.Hidden;
            gridSettingsGPU.Visibility = Visibility.Hidden;
            gridSettingsGeneral.Visibility = Visibility.Hidden;
            gridSettingsMinerConfig.Visibility = Visibility.Visible;
        }

        private void rbSettingsSystem_Checked(object sender, RoutedEventArgs e)
        {
            gridSettingsSystem.Visibility = Visibility.Visible;
            gridSettingsMinerConfig.Visibility = Visibility.Hidden;
            gridSettingsGPU.Visibility = Visibility.Hidden;
            gridSettingsGeneral.Visibility = Visibility.Hidden;
        }

        private void rbSettingsGPU_Checked(object sender, RoutedEventArgs e)
        {
            gridSettingsSystem.Visibility = Visibility.Hidden;
            gridSettingsMinerConfig.Visibility = Visibility.Hidden;
            gridSettingsGPU.Visibility = Visibility.Visible;
            gridSettingsGeneral.Visibility = Visibility.Hidden;
        }

        private void btnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            txtStatusMsg.Text = "Settings have been saved!";
            Properties.Settings.Default.Save();
            Helpers.DebugConsole(txtDebug, "Save/Close button pressed. Settings have been saved!", "Settings");
            btnSaveSettings.Content = "Saved!";
        }

        #region "// SETTINGS GENERAL PAGE //"
        void updateSettings()
        {
            rtbPrevented.Clear();
            rtbAllowed.Clear();

            List<string> gamesList = Properties.Settings.Default.Prevented.Cast<string>().ToList(); // Prevented
            foreach (var prevented in gamesList)
            {
                rtbPrevented.Text += prevented + "\n";
            }

            List<string> gamesIgnored = Properties.Settings.Default.Allowed.Cast<string>().ToList(); // Allowed
            foreach (var allowed in gamesIgnored)
            {
                rtbAllowed.Text += allowed + "\n";
            }
        }

        private void btnAllowedAdd_Click(object sender, RoutedEventArgs e)
        {
            string progAllowAdd = txtAllowedAddInput.Text;
            //MessageBox.Show(txtAllowedAddInput.Text);

            if (progAllowAdd == "")
            {
                txtStatusMsg.Text = "[Allowed|Add] No input provided, try again.";
            }
            else
            {
                Properties.Settings.Default.Allowed.Add(progAllowAdd);
                Properties.Settings.Default.Save();
                txtStatusMsg.Text = String.Format("Allowed entry \"{0}\" added.", progAllowAdd);
            }

            updateSettings();
            txtAllowedAddInput.Clear();
        }

        private void btnAllowedRemove_Click(object sender, RoutedEventArgs e)
        {
            string progAllowRemove = txtAllowedRemoveInput.Text;

            if (progAllowRemove == "")
            {
                txtStatusMsg.Text = "[Allowed|Remove] No input provided, try again.";
            }
            else
            {
                if (Properties.Settings.Default.Allowed.Contains(progAllowRemove))
                {
                    Properties.Settings.Default.Allowed.Remove(progAllowRemove);
                    Properties.Settings.Default.Save();
                    txtStatusMsg.Text = String.Format("Allowed entry \"{0}\" removed.", progAllowRemove);

                    txtAllowedRemoveInput.Clear();
                }
                else
                {
                    txtStatusMsg.Text = String.Format("[Allowed|Remove] Entry \"{0}\" doesn't exist.", progAllowRemove);
                }
            }
            updateSettings();
        }

        private void btnPreventAdd_Click(object sender, RoutedEventArgs e)
        {
            string progPreventAdd = txtPreventAddInput.Text;

            if (progPreventAdd == "")
            {
                txtStatusMsg.Text = "[Prevent|Add] No input provided, try again.";
            }
            else
            {
                Properties.Settings.Default.Prevented.Add(progPreventAdd);
                Properties.Settings.Default.Save();
                txtStatusMsg.Text = String.Format("Prevented entry \"{0}\" added.", progPreventAdd);
            }

            updateSettings();
            txtPreventAddInput.Clear();
        }

        private void btnPreventRemove_Click(object sender, RoutedEventArgs e)
        {
            string progPreventRemove = txtPreventRemoveInput.Text;

            if (progPreventRemove == "")
            {
                txtStatusMsg.Text = "[Prevent|Remove] No input provided, try again.";
            }
            else
            {
                if (Properties.Settings.Default.Prevented.Contains(progPreventRemove))
                {
                    Properties.Settings.Default.Prevented.Remove(progPreventRemove);
                    Properties.Settings.Default.Save();
                    txtStatusMsg.Text = String.Format("Prevented entry \"{0}\" removed.", progPreventRemove);

                    txtPreventRemoveInput.Clear();
                }
                else
                {
                    txtStatusMsg.Text = String.Format("[Prevent|Remove] Entry \"{0}\" doesn't exist.", progPreventRemove);
                }
            }
            updateSettings();
        }

        #endregion

        #region "// SETTINGS MINER ARGUMENTS PAGE //"
        private void txtAdditionalArgs_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerAdditionalArgs = txtAdditionalArgs.Text;
            //Properties.Settings.Default.Save();
        }

        private void txtWorker_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerWorker = txtWorker.Text;
            //Properties.Settings.Default.Save();
        }

        private void txtWallet_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerWallet = txtWallet.Text;
            //Properties.Settings.Default.Save();
        }

        private void txtCoin_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerCoin = txtCoin.Text;
            //Properties.Settings.Default.Save();
        }

        private void txtPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerPassword = txtPassword.Text;
            //Properties.Settings.Default.Save();
        }

        private void txtPool_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerPool = txtPool.Text;
            //Properties.Settings.Default.Save();
        }

        private void rtbMinerArgs_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.MinerArgs = rtbMinerArgs.Text;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }

        private void btnBrowseMiner_Click(object sender, RoutedEventArgs e)
        {
            //ofdMinerPath.InitialDirectory = @"C:\";

            string path = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
            path = System.IO.Path.GetDirectoryName(path);
            path = path + @"\Miner";
            ofdMinerPath.InitialDirectory = path;

            ofdMinerPath.RestoreDirectory = true;
            ofdMinerPath.Title = "Browse for Miner Executable";
            ofdMinerPath.DefaultExt = "exe";
            ofdMinerPath.Filter = "executable files (*.exe)|*.exe|All files (*.*)|*.*";

            ofdMinerPath.ShowDialog();

            if (txtMinerPath.Text != String.Empty)
            {
                txtMinerPath.Text = ofdMinerPath.FileName;

                Helpers.DebugConsole(txtDebug, String.Format("Attempting version check on: {0}", txtMinerPath.Text), "Settings");
                try
                {
                    checkUpdates();
                    Helpers.DebugConsole(txtDebug, String.Format("Version check on {0}: OK.", txtMinerPath.Text), "Settings");
                }
                catch (Exception)
                {
                    Helpers.DebugConsole(txtDebug, String.Format("Version check on {0}: FAIL. (Miner unable to be executed)", txtMinerPath.Text), "Settings");
                }
            }
            else
            {
                ofdMinerPath.FileName = @"No miner provided!";
                txtMinerPath.Text = ofdMinerPath.FileName;
            }

            
            Properties.Settings.Default.MinerPath = txtMinerPath.Text;
            Properties.Settings.Default.Save();

            Helpers.DebugConsole(txtDebug, String.Format("Browsed for miner at: {0}", txtMinerPath.Text), "Settings");
        }

        #endregion

        #region "// SETTINGS SYSTEM PAGE //"
        private void btnRunAtStartup_Click(object sender, RoutedEventArgs e)
        {
            if (btnRunAtStartup.IsChecked == true)
            {
                txtSystemStartup.Text = "Enabled";
                Properties.Settings.Default.RunAtStartup = true;
                try
                {
                    Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    Assembly curAssembly = Assembly.GetExecutingAssembly();
                    key.SetValue(curAssembly.GetName().Name, curAssembly.Location);
                }
                catch (Exception ex)
                {
                    Helpers.DebugConsole(txtDebug, String.Format("Registry error: {0}", ex.GetBaseException()), "Settings");
                }
            }

            if (!btnRunAtStartup.IsChecked == true)
            {
                txtSystemStartup.Text = "Disabled";
                Properties.Settings.Default.RunAtStartup = true;
                try
                {
                    Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    Assembly curAssembly = Assembly.GetExecutingAssembly();
                    key.DeleteValue(curAssembly.GetName().Name);
                }
                catch (Exception ex)
                {
                    Helpers.DebugConsole(txtDebug, String.Format("Registry error: {0}", ex.GetBaseException()), "Settings");
                }
            }
            Properties.Settings.Default.Save();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            
        }

        private void icoTrayIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Show();
            Activate();
            this.WindowState = WindowState.Normal;
            Focus();
        }

        private void btnMinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            if (btnMinimizeToTray.IsChecked == true)
            {
                txtMinimizeToTray.Text = "Enabled";
                Properties.Settings.Default.MinimizeToTray = true;

            }

            if (!btnMinimizeToTray.IsChecked == true)
            {
                txtMinimizeToTray.Text = "Disabled";
                Properties.Settings.Default.MinimizeToTray = false;
            }
            Properties.Settings.Default.Save();
        }

        private void btnRunMinimized_Click(object sender, RoutedEventArgs e)
        {
            if (btnRunMinimized.IsChecked == true)
            {
                txtStartMinimized.Text = "Enabled";
                Properties.Settings.Default.StartMinimized = true;
            }

            if (!btnRunMinimized.IsChecked == true)
            {
                txtStartMinimized.Text = "Disabled";
                Properties.Settings.Default.StartMinimized = false;
            }
            Properties.Settings.Default.Save();
        }
        #endregion

        private void rbAbout_Checked(object sender, RoutedEventArgs e)
        {
            gridMiner.Visibility = Visibility.Hidden;
            gridSettings.Visibility = Visibility.Hidden;
            gridAbout.Visibility = Visibility.Visible;
        }

        private void rbExit_Checked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void txtProcessesSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchProcesses(txtProcessesSearch.Text);
            //Helpers.DebugConsole(txtDebug, "Search query: " + txtProcessesSearch.Text);
        }

        void searchProcesses(string searchQuery)
        {
            //bool found = txtProcesses.Text.ToLower().IndexOf(searchQuery.ToLower()) >= 0;
            try
            {
                var index = txtProcesses.Text.ToLower().IndexOf(searchQuery.ToLower());

                txtProcesses.CaretIndex = index;
                int line = txtProcesses.GetLineIndexFromCharacterIndex(txtProcesses.CaretIndex);
                txtProcesses.ScrollToLine(line);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }
        }

        private void txtMinerVersion_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (UpdateAvailable)
            {
                txtMinerVersion.FontWeight = FontWeights.Bold;
                txtMinerVersion.TextDecorations = TextDecorations.Underline;
                txtMinerVersion.Cursor = System.Windows.Input.Cursors.Hand;
            }
        }

        private void txtMinerVersion_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            txtMinerVersion.FontWeight = FontWeights.Normal;
            //txtMinerVersion.TextDecorations = null;
        }

        private void txtMinerVersion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (UpdateAvailable)
            {
                TextRange document = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);
                document.Text += "\nMinerWatchDog: Updating your miner executable, please wait...\n";
                document.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                try
                {
                    if (MinerRunning)
                    {
                        TextRange minerStoppedMsg = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);
                        minerStoppedMsg.Text += "MinerWatchDog: Miner currently running, force stopping...\n\n";
                        minerStoppedMsg.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                        miner.Kill();
                    }

                    string applicationPath = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
                    applicationPath = System.IO.Path.GetDirectoryName(applicationPath);
                    string updaterPath = applicationPath.Replace(@"file:\", "") + @"\Miner\miner-updater.exe";

                    var minerUpdates = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = updaterPath,
                            WorkingDirectory = applicationPath.Replace(@"file:\", "") + @"\Miner",
                            Arguments = "--update --install --silent phoenixminer",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    minerUpdates.Start();

                    while (!minerUpdates.StandardOutput.EndOfStream)
                    {
                        TextRange updaterOutput = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);
                        updaterOutput.Text = "Miner Updater: " + minerUpdates.StandardOutput.ReadLine() + Environment.NewLine;
                        updaterOutput.ApplyPropertyValue(ForegroundProperty, Brushes.LightGray);
                        txtConsole.ScrollToEnd();
                    }

                    Helpers.DebugConsole(txtDebug, "Application Path: " + applicationPath.Replace(@"file:\", ""), "Updater");

                }
                catch (Exception ex)
                {
                    Helpers.DebugConsole(txtDebug, "Error: " + ex.GetBaseException(), "Updater");
                }

                TextRange verifyUpdateMsg = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);
                verifyUpdateMsg.Text += "\nMinerWatchDog: Verifying update...";
                verifyUpdateMsg.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));

                string applicationPath2 = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
                applicationPath2 = System.IO.Path.GetDirectoryName(applicationPath2);
                string localMiner = checkLocalMinerVersion(silent: false, updaterPath: applicationPath2.Replace(@"file:\", "") + @"\Miner\PhoenixMiner.exe");

                TextRange verifiedUpdateMsg = new TextRange(txtConsole.Document.ContentEnd, txtConsole.Document.ContentEnd);
                verifiedUpdateMsg.Text += "\nMinerWatchDog: " + localMiner;
                verifiedUpdateMsg.Text += "\nMinerWatchDog: Updating miner path to " + applicationPath2.Replace(@"file:\", "") + @"\Miner\PhoenixMiner.exe";

                txtMinerPath.Text = applicationPath2.Replace(@"file:\", "") + @"\Miner\PhoenixMiner.exe";
                Properties.Settings.Default.MinerPath = txtMinerPath.Text;

                verifiedUpdateMsg.Text += "\nMinerWatchDog: Update is now complete, restarting miner...\n";
                verifiedUpdateMsg.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));

                checkUpdates();

                MessageBox.Show("Please note, since the auto updater downloads crypto mining software (PhoenixMiner), windows defender does not take too kindly to it. " + 
                    "In the event the updater is not successful, its likely windows defender is blocking the PhoenixMiner.zip, so in order to fix this you need to allow it " + 
                    "in defender. \n\nIf you notice significant differences in performance (ex. unusually high ram usage, far lower hashrate, etc.) " +
                    "or you notice the watchdog is not working as intended after the auto-update, simply restart.", "[Important]", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                miner.Start();
                Thread tProcMiner = new Thread(refreshMinerOutput);
                tProcMiner.IsBackground = true;
                tProcMiner.Start();
            }
        }

        private void hkGaming_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.HotkeyGaming = hkGaming.HotkeyTextBox.Text;
            Properties.Settings.Default.Save();
            txtHkGaming.Text = Properties.Settings.Default.HotkeyGaming;
        }

        private void hkMining_TextChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.HotkeyMining = hkMining.HotkeyTextBox.Text;
            Properties.Settings.Default.Save();
            txtHkMining.Text = Properties.Settings.Default.HotkeyMining;
        }

        private void lbCheckUpdates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.UpdateIntervalSelectedIndex = lbCheckUpdates.SelectedIndex;
            string text = (lbCheckUpdates.SelectedItem as ComboBoxItem).Content.ToString();
            UpdateInterval = Int32.Parse(text) * 3600000; // 1 hr * 3600000 = time in ms

            Properties.Settings.Default.UpdateInterval = UpdateInterval;
            Properties.Settings.Default.Save();

            Helpers.DebugConsole(txtDebug, String.Format("Update interval check has been set to: {0}ms [{1} hour(s)]", UpdateInterval, text), "Updater");
            Helpers.DebugConsole(txtDebug, String.Format("Update interval index has been set to: {0}", lbCheckUpdates.SelectedIndex), "Updater");
        }

        private void txtMaxIncorrectShares_TextChanged(object sender, TextChangedEventArgs e)
        {
            try { MaxInvalidShares = Int32.Parse(txtMaxIncorrectShares.Text); }
            catch (Exception) { Helpers.DebugConsole(txtDebug, "Parse max incorrect shares textbox input to int failed", "Settings"); }

            Properties.Settings.Default.MaxInvalidShares = MaxInvalidShares;
            Properties.Settings.Default.Save();
        }

        private void btnConfigImporter_Click(object sender, RoutedEventArgs e)
        {
            ConfigImporter configImporterForm = new ConfigImporter(this);
            configImporterForm.Show();
            btnConfigImporter.IsEnabled = false;
            btnConfigImporter.Content = "Running";
        }

        private void txtMinerPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkUpdates();
        }

        private void btnOpenDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(txtSupportLink.Text);
        }

        private void btnSeeMore_Click(object sender, RoutedEventArgs e)
        {
            if (latestWatchdogRelease != null || latestWatchdogRelease == "")
            {
                MessageBoxResult result = MessageBox.Show(latestWatchdogChangelog + "\n\nWould you like to download the installer for the latest version?\n(Opens link to Github)", "An update for Miner Watchdog 3 is available.", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(String.Format(@"https://github.com/ShoobyDoo/MinerWatchDog3/releases/tag/{0}", latestWatchdogRelease));
                }
            }
        }

        private void txtVersion_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            txtVersion.FontWeight = FontWeights.Normal;
            txtVersion.TextDecorations = null;
        }

        private void txtVersion_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Version latest = new Version(latestWatchdogRelease);
            if (latest > Assembly.GetExecutingAssembly().GetName().Version)
            {
                txtVersion.FontWeight = FontWeights.Bold;
                txtVersion.TextDecorations = TextDecorations.Underline;
                txtVersion.Cursor = System.Windows.Input.Cursors.Hand;
            }
        }

        private void txtVersion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Version latest = new Version(latestWatchdogRelease);
            if (latest > Assembly.GetExecutingAssembly().GetName().Version)
            {
                rbAbout.IsChecked = true;
                rbAbout_Checked(sender, e);
            }
        }

        private void bdrUpdateAvailable_MouseDown(object sender, MouseButtonEventArgs e)
        {
            btnSeeMore_Click(sender, e);
        }

        private void bdrUpdateAvailable_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            txtUpdateAvailable.TextDecorations = TextDecorations.Underline;
            txtUpdateAvailable.FontWeight = FontWeights.UltraBold;
            bdrUpdateAvailable.Cursor = System.Windows.Input.Cursors.Hand;
        }

        private void bdrUpdateAvailable_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            txtUpdateAvailable.TextDecorations = null;
            bdrUpdateAvailable.Cursor = System.Windows.Input.Cursors.Arrow;
            txtUpdateAvailable.FontWeight = FontWeights.Bold;
        }
    }
}
