using System.Collections.Generic;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public interface ITechnicianReportStateService
    {
        Task SaveState(SavedTechnicianReportState state, string reportName);
        Task<SavedTechnicianReportState?> LoadState(string reportName);
        Task<List<string>> GetSavedReportIds();
        Task DeleteState(string reportName);
        Task<bool> ReportExists(string reportName);
    }
}