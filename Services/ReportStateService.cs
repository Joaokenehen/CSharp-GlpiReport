using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public class ReportStateService : IReportStateService
    {
        private readonly string _reportsPath;

        public ReportStateService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "RelatorioGLPIApp");
            _reportsPath = Path.Combine(appFolder, "SavedReports");
            Directory.CreateDirectory(_reportsPath);
        }

        public async Task SaveState(SavedReportState state, string finalFileName)
        {
            var filePath = Path.Combine(_reportsPath, finalFileName);

            var jsonState = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, jsonState);
        }

        public async Task<SavedReportState?> LoadState(string reportId)
        {
            var filePath = Path.Combine(_reportsPath, reportId);
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<SavedReportState>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task<List<string>> GetSavedReportIds()
        {
            var files = Directory.GetFiles(_reportsPath, "*.json")
                                 .Select(Path.GetFileName)
                                 .Where(f => f != null)
                                 .ToList();
            return Task.FromResult(files!);
        }

        public Task<bool> ReportExists(string reportName)
        {
            var finalFileName = GetFinalFileName(reportName);
            var filePath = Path.Combine(_reportsPath, finalFileName);
            return Task.FromResult(File.Exists(filePath));
        }

        private string GetFinalFileName(string reportName)
        {
            var sanitizedName = string.Join("_", reportName.Split(Path.GetInvalidFileNameChars()));
            return string.IsNullOrWhiteSpace(sanitizedName) ? $"Relatorio_sem_nome_{DateTime.Now:yyyyMMddHHmmss}.json" : $"{sanitizedName}.json";
        }
    }
}