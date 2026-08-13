using System.Collections.Generic;
using RelatorioGLPIApp.ViewModels;

namespace RelatorioGLPIApp.Models
{
    public class SavedTechnicianReportState
    {
        public List<TechnicianStat> TechnicianStats { get; set; } = new();
        public bool IsDailyReport { get; set; }
    }
}