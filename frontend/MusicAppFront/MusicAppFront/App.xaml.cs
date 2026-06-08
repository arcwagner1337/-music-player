using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;


namespace MusicAppFront
{

    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string envPath = File.Exists(".env")
                 ? ".env"
                 : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\..\", ".env");

            if (File.Exists(envPath))
            {



                try
                {
                    
                    DotNetEnv.Env.Load(envPath);

                    
                    Settings = new AppSettings
                    {
                        DlpServerUrlLog1 = Environment.GetEnvironmentVariable("DLP_SERVER_LOG1_URL")
                            ?? throw new Exception("DLP_SERVER_LOG1_URL не задан в .env"),

                        DlpServerUrlLog2 = Environment.GetEnvironmentVariable("DLP_SERVER_LOG2_URL")
                            ?? throw new Exception("DLP_SERVER_LOG2_URL не задан в .env"),

                        DlpServerUrlUnlog1 = Environment.GetEnvironmentVariable("DLP_SERVER_UNLOG1_URL")
                            ?? throw new Exception("DLP_SERVER_UNLOG1_URL не задан в .env"),

                        DlpServerUrlUnlog2 = Environment.GetEnvironmentVariable("DLP_SERVER_UNLOG2_URL")
                            ?? throw new Exception("DLP_SERVER_UNLOG2_URL не задан в .env"),

                        BaseAddress = Environment.GetEnvironmentVariable("BASE_ADDRESS")
                            ?? throw new Exception("BASE_ADDRESS не задан в .env")
                    };
                }
                catch (Exception ex)
                {
                    
                    MessageBox.Show($"Критическая ошибка конфигурации: {ex.Message}", "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

            }
            else
            {
                throw new Exception($"Файл .env не найден ни в папке сборки, ни в корне проекта по пути: {Path.GetFullPath(envPath)}");
            }

        }


    }
}
