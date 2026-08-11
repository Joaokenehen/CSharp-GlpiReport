using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public interface IReportStateService
    {
        Task SaveState(SavedReportState state, string reportName);
        Task<SavedReportState?> LoadState(string reportId);
        Task<List<string>> GetSavedReportIds();
        Task<bool> ReportExists(string reportName);
        Task Delete(string reportId);
    }
}