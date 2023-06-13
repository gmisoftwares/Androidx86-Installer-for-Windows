using System;
using System.IO;
using System.Windows;

namespace Android_UEFIInstaller
{
    public static class Log
    {
        private static System.Windows.Controls.TextBox _buffer;
        private static System.Windows.Controls.TextBlock _lstatus;
        private static string _lbuffer;

        public static void write(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _buffer.AppendText(text + Environment.NewLine);
            });
            _lbuffer += (text + Environment.NewLine);
        }

        public static void updateStatus(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _lstatus.Text = text;
            }); 
        }

        public static void save()
        {

            string filePath = string.Format(config.LOG_FILE_PATH, DateTime.Now.Millisecond);
            File.WriteAllText(filePath, _lbuffer);
        }

        public static void SetLogBuffer(System.Windows.Controls.TextBox buffer)
        {
            _buffer = buffer;
        }

        public static void SetStatuslabel(System.Windows.Controls.TextBlock buffer)
        {
            _lstatus = buffer;
        }
    }
}
