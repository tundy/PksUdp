using System;
using System.IO;
using System.Windows;

namespace PksGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            if (ex == null) return;
            File.WriteAllText($"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.dmp", ex.ToString());

        }
    }
}
