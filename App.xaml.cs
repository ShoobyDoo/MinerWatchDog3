using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ModernMinerWatchDog
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string logname = String.Format("crashlog-{0}.txt", DateTime.Now.ToString("MMddyyyy_HH-mm"));
            string error = "FATAL (message)\n---------------\n\n" +
                e.Exception.Message +
                "\n\nFATAL (stacktrace)\n------------------\n\n" +
                e.Exception.StackTrace + "\n\nEND.";

            string folderLogs = Environment.CurrentDirectory + @"\Logs";
            string folderLogsCrash = Environment.CurrentDirectory + @"\Logs\crash";

            if (!Directory.Exists(folderLogs)) { Directory.CreateDirectory(folderLogs); }
            if (!Directory.Exists(folderLogsCrash)) { Directory.CreateDirectory(folderLogsCrash); }

            string crashlog = folderLogsCrash + @"\" + logname;
            File.WriteAllText(crashlog, error);
        }
    }
}
