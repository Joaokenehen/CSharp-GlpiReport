using System.Collections.Generic;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services;

public interface IGeneralReportStateService
{
    Task SaveState(SavedGeneralReportState state, string reportName);
    Task<SavedGeneralReportState?> LoadState(string reportId);
    Task<List<string>> GetSavedReportIds();
    Task<bool> ReportExists(string reportName);
    Task DeleteState(string reportId);
}