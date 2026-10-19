using System.Configuration;
using System.Windows;

namespace WpfLoginApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Подключение к MS SQL Server настраивается в App.config (ConnectionStrings/LoginDb)
    }
}
