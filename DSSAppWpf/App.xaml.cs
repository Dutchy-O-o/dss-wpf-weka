using System;
using System.Windows;
using System.Windows.Threading;

namespace DSSAppWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(ex.Exception.ToString(), "Beklenmeyen Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };
            base.OnStartup(e);
        }
    }
}
