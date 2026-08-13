using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public class TechnicianReportStateService : ITechnicianReportStateService
    {
        private readonly string _reportsDirectory;

        public TechnicianReportStateService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDirectory = Path.Combine(appDataPath, "RelatorioGLPIApp");
            _reportsDirectory = Path.Combine(appDirectory, "technician_reports"); // Different folder
            Directory.CreateDirectory(_reportsDirectory);
        }

        public async Task<List<string>> GetSavedReportIds()
        {
            var files = await Task.Run(() =>
                Directory.GetFiles(_reportsDirectory, "*.json")
                         .Select(Path.GetFileName)
                         .OfType<string>()
                         .ToList());
            return files;
        }

        public async Task<SavedTechnicianReportState?> LoadState(string reportName)
        {
            var filePath = Path.Combine(_reportsDirectory, reportName);
            if (!File.Exists(filePath)) return null;

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<SavedTechnicianReportState>(json);
        }

        public async Task SaveState(SavedTechnicianReportState state, string reportName)
        {
            var sanitizedName = string.Join("_", reportName.Split(Path.GetInvalidFileNameChars()));
            var finalFileName = string.IsNullOrWhiteSpace(sanitizedName) ? $"Relatorio_Tecnico_{DateTime.Now:yyyyMMddHHmmss}.json" : $"{sanitizedName}.json";
            var filePath = Path.Combine(_reportsDirectory, finalFileName);
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task DeleteState(string reportName)
        {
            var filePath = Path.Combine(_reportsDirectory, reportName);
            await Task.Run(() => { if (File.Exists(filePath)) File.Delete(filePath); });
        }

        public async Task<bool> ReportExists(string reportName)
        {
            var sanitizedName = string.Join("_", reportName.Split(Path.GetInvalidFileNameChars()));
            var finalFileName = $"{sanitizedName}.json";
            var filePath = Path.Combine(_reportsDirectory, finalFileName);
            return await Task.FromResult(File.Exists(filePath));
        }
    }
}