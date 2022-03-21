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
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ModernMinerWatchDog
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _hotkeyTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _settingsTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _minerUpdater = new System.Windows.Forms.Timer();
        System.Windows.Forms.NotifyIcon icoTrayIcon = new System.Windows.Forms.NotifyIcon();
        System.IO.Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Images/miner-icon.ico")).Stream;
        OpenFileDialog ofdMinerPath = new OpenFileDialog();
        Process miner = new Process();
        
        public bool MinerRunning { get; set; }
        public bool UpdateAvailable { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Closing += onExit;

            _timer.Interval = 500;
            _timer.Tick += refreshMinerArgs;
            _timer.Enabled = true;

            _minerUpdater.Interval = 43200000; // every 12 hours
            _minerUpdater.Tick += delegate { checkUpdates(); };
            _minerUpdater.Enabled = true;

            _settingsTimer.Interval = 1500;
            _settingsTimer.Tick += delegate { if (btnSaveSettings.Content.ToString() == "Saved!") { btnSaveSettings.Content = "Save"; } };
            _settingsTimer.Enabled = true;

            icoTrayIcon.Icon = new System.Drawing.Icon(iconStream);
            icoTrayIcon.Text = "Miner WatchDog 3";
            icoTrayIcon.MouseDoubleClick += icoTrayIcon_MouseDoubleClick;

            // Main miner process info
            miner.StartInfo.RedirectStandardOutput = true;
            miner.StartInfo.UseShellExecute = false;
            miner.StartInfo.CreateNoWindow = true;

            hkGaming.HotkeyTextBox.TextChanged += hkGaming_TextChanged;
            hkMining.HotkeyTextBox.TextChanged += hkMining_TextChanged;
        }

        private void onExit(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
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
            }
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

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;

            if (Properties.Settings.Default.MinimizeToTray == true)
            {
                if (WindowState.Minimized == this.WindowState)
                {
                    Helpers.DebugConsole(txtDebug, "Program has been sent to tray", "Settings");
                    icoTrayIcon.BalloonTipText = "Monitoring running processes in the background...";
                    icoTrayIcon.BalloonTipTitle = "Miner WatchDog 3";
                    icoTrayIcon.BalloonTipIcon = ToolTipIcon.Info; 
                    //icoTrayIcon.BalloonTipIcon = iconStream;

                    icoTrayIcon.Visible = true;
                    icoTrayIcon.ShowBalloonTip(5);

                    Hide();
                }

                else if (WindowState.Normal == this.WindowState)
                {
                    icoTrayIcon.Visible = false;
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

        private void Window_Activated(object sender, EventArgs e)
        {

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
                        //DebugConsole("INTENSE          : " + p.ProcessName);

                        processStatus = "A GPU intensive program is currently running...";
                        processList += " " + p.Id.ToString().PadRight(20) + p.ProcessName + "\n";
                        gamesAllowed = false;
                        break;
                    }
                    else if (gamesIgnored.Contains(p.ProcessName))
                    {
                        //DebugConsole("NON INTENSE      : " + p.ProcessName);

                        processStatus = "A Non-GPU intensive program is currently running...";
                        processList += " " + p.Id.ToString().PadRight(20) + p.ProcessName + "\n";
                        gamesAllowed = true;
                        parentFound = true;
                    }

                    if (!parentFound)
                    {
                        //DebugConsole("NO GAMES RUNNING : " + p.ProcessName);

                        processStatus = "No blacklisted program(s) currently running...";
                        allProcesses += " " + p.Id.ToString().PadRight(20) + p.ProcessName + "\n";
                        gamesAllowed = false;
                        parentFound = false;
                    }
                    else
                    {
                        REFRESH_INTERVAL = 0;
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

                        string presetMiner = switchMinerProfile(Properties.Settings.Default.HotkeyGaming);

                        this.Dispatcher.Invoke(() =>
                        {
                            Helpers.DebugConsole(txtDebug, "Experimental: Attempting hot-key based clock preset for non-miner related task...");

                            TextRange document = new TextRange(
                                // TextPointer to the start of content in the RichTextBox.
                                txtConsole.Document.ContentEnd,
                                // TextPointer to the end of content in the RichTextBox.
                                txtConsole.Document.ContentEnd
                            );
                            //TextRange document = new TextRange(txtConsole.Document.ContentStart, txtConsole.Document.ContentEnd);
                            document.Text += "\nMinerWatchDog: Miner has been automatically stopped due to a gpu-intensive program\nPresetController: (Context: " + presetMiner + ")\n";
                            document.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                            txtConsole.ScrollToEnd();

                            Helpers.DebugConsole(txtDebug, "Miner process killed (" + Properties.Settings.Default.MinerPath + ")", "Main");
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

                            string presetMiner = switchMinerProfile(Properties.Settings.Default.HotkeyMining);

                            this.Dispatcher.Invoke(() => 
                            {
                                Helpers.DebugConsole(txtDebug, "Experimental: Attempting hot-key based clock preset for non-miner related task...");

                                TextRange document = new TextRange(
                                // TextPointer to the start of content in the RichTextBox.
                                txtConsole.Document.ContentEnd,
                                // TextPointer to the end of content in the RichTextBox.
                                txtConsole.Document.ContentEnd
                                );
                                //TextRange document = new TextRange(txtConsole.Document.ContentStart, txtConsole.Document.ContentEnd);
                                document.Text += "MinerWatchDog: Miner has been automatically started\nPresetController: (Context: " + presetMiner + ")\n";
                                document.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));

                                Helpers.DebugConsole(txtDebug, "Miner process started (" + Properties.Settings.Default.MinerPath + ")", "Main"); 
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

                //Console.WriteLine(minerStatus);

                this.Dispatcher.Invoke(() =>
                {
                    TextRange rangeStatus = new TextRange(txtStatus.Document.ContentStart, txtStatus.Document.ContentEnd);
                    rangeStatus.Text = processStatus.ToString();

                    TextRange rangeMinerStatus = new TextRange(txtMinerStatus.Document.ContentStart, txtMinerStatus.Document.ContentEnd);
                    rangeMinerStatus.Text = minerStatus.ToString();

                    RichTextManipulation.FromTextPointer(txtMinerStatus.Document.ContentStart, txtMinerStatus.Document.ContentEnd, "ONLINE 🔴", Brushes.LimeGreen);
                    RichTextManipulation.FromTextPointer(txtMinerStatus.Document.ContentStart, txtMinerStatus.Document.ContentEnd, "OFFLINE 🔴", Brushes.Salmon);

                    //txtStatus.Text = processStatus;
                    //txtMinerStatus.Text = minerStatus;
                    //TextRange processes = new TextRange(txtProcesses.Document.ContentStart, txtProcesses.Document.ContentEnd);

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

            //Helpers.DebugConsole(txtDebug, "Checking to see if miner has updates...", "Updater");

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
                        minerOutputLine.Text.StartsWith("Eth: Effective")
                        )
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Yellow);
                    }
                    else if (minerOutputLine.Text.StartsWith("Eth: Incorrect"))
                    {
                        minerOutputLine.ApplyPropertyValue(ForegroundProperty, Brushes.Red);
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

                        //Helpers.DebugConsole(txtDebug, "", "Main");
                    }

                    //txtConsole.SelectionStart = txtConsole.SelectAll();
                    txtConsole.ScrollToEnd();

                    //txtDebug.SelectionStart = txtDebug.Text.Length;
                    //txtDebug.ScrollToEnd();
                });
            }
        }

        private void txtConsole_TextChanged(object sender, TextChangedEventArgs e)
        {
            
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
                    catch (Exception)
                    {
                    }

                }
            }
        }

        public string checkMinerUpdate(bool silent = false)
        {
            string silentFlag = "";
            var line = "";

            Helpers.DebugConsole(txtDebug, "Checking to see if miner has updates...", "Updater");

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
                    Helpers.DebugConsole(txtDebug, line, "Changelog");

                    //break;
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
            catch (Exception ex)
            {
                Helpers.DebugConsole(txtDebug, "An error occurred while attempting to check miner version! Stacktrace: " + ex.GetBaseException(), level: 2);
            }

            return line.Trim();
        }

        public string checkLatestChangelog(bool silent = false)
        {
            string silentFlag = "";
            var line = "";

            //Helpers.DebugConsole(txtDebug, "Checking to see if miner has updates...", "Updater");

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

        void checkUpdates()
        {
            string localMiner = checkLocalMinerVersion(silent: true);
            string latestMiner = checkMinerUpdate(silent: true);
            string latestChangelog = checkLatestChangelog(silent: true);
            Helpers.DebugConsole(txtDebug, "Checking for changelog... " + latestChangelog, "Updater");


            if (localMiner.Trim() == latestMiner.Trim())
            {
                tbTTUpdateChangelog.Text = "You're on the latest version.";
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
                txtMinerVersion.Text = String.Format("Status: Update available! ({0})", latestMiner.Trim());
                txtMinerVersion.Foreground = Brushes.LightGoldenrodYellow;
                txtMinerVersion.TextDecorations = TextDecorations.Underline;
                UpdateAvailable = true;

                // TODO here.
                tbTTUpdateChangelog.Text = "Here are some details about the new update:\n\n" + latestChangelog;
            }

            txtLocalMiner.Text = "Miner: PhoenixMiner " + localMiner;

            Helpers.DebugConsole(txtDebug, "Checking for updates... ", "Updater");
            Helpers.DebugConsole(txtDebug, "Local miner: " + localMiner, "Updater");
            Helpers.DebugConsole(txtDebug, "Latest miner: " + latestMiner, "Updater");
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
                }
            }
            else
            {
                Helpers.Debug = false;
            }

            Helpers.DebugConsole(txtDebug, "Application loaded successfully.");

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
                publishVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
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

            Helpers.DebugConsole(txtDebug, "Settings loaded successfully.", "Settings");
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

        private void icoTrayIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
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
                verifiedUpdateMsg.Text += "\nMinerWatchDog: Update is now complete, attempting to restart (experimental)...\n";
                verifiedUpdateMsg.ApplyPropertyValue(ForegroundProperty, new SolidColorBrush(Color.FromRgb(23, 162, 184)));

                checkUpdates();

                MessageBox.Show("Note:\n\nIf you notice significant differences in performance (ex. unusually high ram usage, far lower hashrate, etc.) " +
                    "or you notice the watchdog is not working as intended after the auto-update, simply restart.");

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

    }
}
