using System;
using System.IO;
using System.Text.Json;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _filePath;

        public bool IsOfflineMode { get; set; } = false;

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "RelatorioGLPIApp");
            Directory.CreateDirectory(appFolder);
            _filePath = Path.Combine(appFolder, "settings.json");
        }

        public void SaveCredentials(LoginCredentials credentials)
        {
            var json = JsonSerializer.Serialize(credentials);
            File.WriteAllText(_filePath, json);
        }

        public LoginCredentials? LoadCredentials()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<LoginCredentials>(json);
            }
            catch (Exception) // Lida com arquivo corrompido ou inacessível
            {
                return null;
            }
        }

        public void ClearCredentials()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}
