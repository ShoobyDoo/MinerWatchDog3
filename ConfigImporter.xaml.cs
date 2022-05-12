using Microsoft.Win32;
using ModernMinerWatchDog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace ModernMinerWatchDog
{
    /// <summary>
    /// Interaction logic for ConfigImporter.xaml
    /// </summary>
    public partial class ConfigImporter
    {
        MainWindow mainWindow;

        string[] localAppdataOLD = Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Doomlad");
        string[] localAppdataNEW = Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\MinerWatchDog3");
        List<Configs> configs = new List<Configs>();
        Dictionary<string, string> importedBatchScript = new Dictionary<string, string>();

        public string OldConfigPath { get; set; }
        public string ImportedBatchScript { get; set; }

        public ConfigImporter(MainWindow mw)
        {
            InitializeComponent();
            mainWindow = mw;
            Closing += ConfigImporter_Closing;

            List<string> masterDirectories = localAppdataOLD.Concat(localAppdataNEW).ToList();
            foreach (string appInstance in masterDirectories)
            {
                foreach (string version in Directory.GetDirectories(appInstance))
                {
                    configs.Add(new Configs() { wdVersion = new Version(version.Split('\\').Last()), configPath = version });
                }
            }

            lvConfigs.ItemsSource = configs;
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvConfigs.ItemsSource);
            view.Filter = UserFilter;

            txtTotal.Text = String.Format("Total configs found: {0}", lvConfigs.Items.Count);
        }

        public class Configs
        {
            public Version wdVersion { get; set; }
            public string configPath { get; set; }
        }

        private bool UserFilter(object item)
        {
            if (String.IsNullOrEmpty(txtFilter.Text))
            {
                return true;
            }
            else
            {
                return (item as Configs).configPath.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(lvConfigs.ItemsSource).Refresh();
            txtTotal.Text = String.Format("Total configs found: {0}", lvConfigs.Items.Count);
        }

        private void ConfigImporter_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mainWindow.btnConfigImporter.IsEnabled = true;
            mainWindow.btnConfigImporter.Content = "Launch";
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void lvConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Version myWdVersion = (lvConfigs.SelectedItem as Configs).wdVersion;
                OldConfigPath = (lvConfigs.SelectedItem as Configs).configPath;

                txtSelection.Text = String.Format("Selection: {0}", myWdVersion.ToString());
            }
            catch (Exception ex)
            {
                Helpers.DebugConsole(mainWindow.txtDebug, ex.Message, "CfgImporter", 2);
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            if (OldConfigPath != null)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                string[] localAppdata = Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\MinerWatchDog3");
                string allConfigsPath = localAppdata[0];
                string configFilename = "\\user.config";
                string currentConfigPath = "";

                foreach (string version in Directory.GetDirectories(allConfigsPath))
                {
                    Version _current = new Version(version.Split('\\').Last());

                    if (_current == Assembly.GetExecutingAssembly().GetName().Version)
                    {
                        currentConfigPath = version + configFilename;
                    }
                }

                try
                {
                    if (OldConfigPath + configFilename == currentConfigPath)
                    {
                        MessageBox.Show("You are trying to import the configuration the application is currently using, aborting.", "Error!");
                        Mouse.OverrideCursor = Cursors.Arrow;
                        return;
                    }
                    else
                    {
                        File.Copy(OldConfigPath + configFilename, currentConfigPath, true);
                        Helpers.DebugConsole(mainWindow.txtDebug, String.Format("Copied {0} -> {1}", OldConfigPath + configFilename, currentConfigPath), "Config");
                    }
                }
                catch (Exception ex)
                {
                    Helpers.DebugConsole(mainWindow.txtDebug, "An error occurred while attempting to copy configs, see: " + ex.Message, "CfgImporter", 2);
                }

                MessageBox.Show("Previous settings were successfully copied over to new installation.\n\n" + 
                    "MinerWatchDog3 will now restart to complete the import.\n(Give it a second to restart, if it doesn't just click import again.)", "Success!");

                Mouse.OverrideCursor = Cursors.Arrow;
                System.Windows.Forms.Application.Restart();
            }
        }

        private void dragBorder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Assuming you have one file that you care about, 
                // pass it off to whatever handling code you have defined.
                processFileImport(files[0]);
            }
        }
        
        private void processFileImport(string filepath)
        {
            Helpers.DebugConsole(mainWindow.txtDebug, "Drag/Drop config import requested at file: " + filepath, "CfgImporter");
            
            StreamReader SR = new StreamReader(filepath);
            string strFileText = SR.ReadToEnd();
            SR.Close();
            SR.Dispose();

            List<string> _filePath = filepath.Split('\\').ToList();
            _filePath.Remove(_filePath[_filePath.Count - 1]);

            string _minerPath = String.Join("\\", _filePath);

            Properties.Settings.Default.MinerPath = _minerPath + @"\PhoenixMiner.exe";

            strFileText = strFileText.Replace("pause", "").Replace("PhoenixMiner.exe", "").Trim();

            string[] args = strFileText.Split();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains("-"))
                {
                    importedBatchScript.Add(args[i], args[i + 1]);
                }
            }

            string tmpExtraArgs = "";
            foreach (KeyValuePair<string, string> kvp in importedBatchScript)
            {
                if (kvp.Key == "-pool") 
                { 
                    Properties.Settings.Default.MinerPool = kvp.Value;
                }
                else if (kvp.Key == "-wal") 
                { 
                    Properties.Settings.Default.MinerWallet = kvp.Value;
                }
                else if (kvp.Key == "-worker") 
                { 
                    Properties.Settings.Default.MinerWorker = kvp.Value;
                }
                else if (kvp.Key == "-epsw") 
                { 
                    Properties.Settings.Default.MinerPassword = kvp.Value;
                }
                else if (kvp.Key == "-coin") 
                {
                    Properties.Settings.Default.MinerCoin = kvp.Value;
                }
                else 
                { 
                    tmpExtraArgs += string.Format("{0} {1} ", kvp.Key, kvp.Value);
                    Properties.Settings.Default.MinerAdditionalArgs = tmpExtraArgs;
                }
            }

            string[] minerSettings = mainWindow.listOfSettings();
            foreach (string i in minerSettings) { Helpers.DebugConsole(mainWindow.txtDebug, i, "CfgImporter"); }

            MessageBox.Show(String.Format("<----- Parsed {0} items successfully, see configuration below ----->\n\n{1}\n\n" + 
                "If you need to add or modify any of the settings imported above, please see the miner configuration tab in the settings menu.", 
                importedBatchScript.Count, String.Join("\n", minerSettings)), "Batch File Parser");
        }

        private void dragBorder_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.Filter = "Windows Batch File (*.bat)|*.bat";
            openFileDialog.DefaultExt = ".bat";
            openFileDialog.RestoreDirectory = true;

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string configFilePath = openFileDialog.FileName;
                if (configFilePath != "")
                {
                    processFileImport(configFilePath);
                }
            }
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            dragBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#736656"));
            dragIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#736656"));
            dragText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#736656"));
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            dragBorder.BorderBrush = new SolidColorBrush(Colors.AliceBlue);
            dragIcon.Foreground = new SolidColorBrush(Colors.AliceBlue);
            dragText.Foreground = new SolidColorBrush(Colors.AliceBlue);
        }

        private void Button_DragEnter(object sender, DragEventArgs e)
        {
            dragBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#736656"));
            dragIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#736656"));
            dragText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#736656"));
        }

        private void Button_DragLeave(object sender, DragEventArgs e)
        {
            dragBorder.BorderBrush = new SolidColorBrush(Colors.AliceBlue);
            dragIcon.Foreground = new SolidColorBrush(Colors.AliceBlue);
            dragText.Foreground = new SolidColorBrush(Colors.AliceBlue);
        }
    }
}
