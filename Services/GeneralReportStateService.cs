using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services;

public class GeneralReportStateService : IGeneralReportStateService
{
    private readonly string _reportsPath;
    public GeneralReportStateService(bool isOffline = false)
    {
        var baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RelatorioGLPIApp");
        var environmentFolder = isOffline ? "Offline" : "Online";
        _reportsPath = Path.Combine(baseFolder, environmentFolder, "SavedGeneralReports");
        if (!Directory.Exists(_reportsPath))
        {
            Directory.CreateDirectory(_reportsPath);
        }
    }

    public async Task SaveState(SavedGeneralReportState state, string reportName)
    {
        var finalFileName = GetFinalFileName(reportName);
        var filePath = Path.Combine(_reportsPath, finalFileName);
        var jsonState = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, jsonState);
    }

    public async Task<SavedGeneralReportState?> LoadState(string reportId)
    {
        var filePath = Path.Combine(_reportsPath, reportId);
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<SavedGeneralReportState>(json);
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

    public Task DeleteState(string reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return Task.CompletedTask;

        var filePath = Path.Combine(_reportsPath, reportId);
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }

    private string GetFinalFileName(string reportName)
    {
        var sanitizedName = string.Join("_", reportName.Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(sanitizedName) ? $"RelatorioGeral_sem_nome_{DateTime.Now:yyyyMMddHHmmss}.json" : $"{sanitizedName}.json";
    }
}