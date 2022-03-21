using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ModernMinerWatchDog.Core
{
    class Helpers
    {
        public static bool Debug { get; set; }
        /// <summary>
        /// Prints console messages to debug tab textbox.
        /// <br>1=WARN</br>
        /// <br>2=ERROR</br>
        /// </summary>
        /// <param name="message">Desired message to print to debug console</param>
        /// <param name="file">File which DebugConsole was accessed from</param>
        /// <param name="level">Desired debug level. 0=INFO, 1=WARN, 2=ERROR</param>
        public static void DebugConsole(TextBox control, string message, string file = "Main", int level = 0)
        {
            string timeStamp = DateTime.Now.ToString("hh:mm:ss tt");
            string prefix = "";

            if (level == 0)
            {
                prefix = string.Format("[{0}] [{1}] [INFO ] : ", timeStamp, file.PadRight(11, ' '));
            }
            else if (level == 1)
            {
                prefix = string.Format("[{0}] [{1}] [WARN ] : ", timeStamp, file.PadRight(11, ' '));
            }
            else if (level == 2)
            {
                prefix = string.Format("[{0}] [{1}] [ERROR] : ", timeStamp, file.PadRight(11, ' '));
            }

            if (Debug)
            {
                control.Text += prefix + message + "\n";
            }
            else
            {
                control.Text = "Program is running in production mode, debugging messages are disabled.\nRun program with the --debug flag to enable debugging.";
            }

        }
    }
}
