using System.Collections.Generic;
using RelatorioGLPIApp.ViewModels;

namespace RelatorioGLPIApp.Models
{
    public record TechnicianReportPdfModel(
        ICollection<TechnicianStat> TechnicianStats
    );
}