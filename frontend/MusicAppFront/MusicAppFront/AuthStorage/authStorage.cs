using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicAppFront.AuthStorage
{
    public static class AuthStorage
    {
        private static readonly string StoragePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicAppFront", // Название твоего проекта
            "auth.bin"
        );

        public static void SaveToken(string token)
        {
            // Создаем папку, если её нет
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(StoragePath));

            // Пишем токен в файл
            File.WriteAllText(StoragePath, token);
        }

        public static string GetToken()
        {
            if (!File.Exists(StoragePath)) return null;
            return File.ReadAllText(StoragePath);
        }

        public static void Clear()
        {
            if (File.Exists(StoragePath)) File.Delete(StoragePath);
        }
    }
}
