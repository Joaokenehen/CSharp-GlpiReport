using System;
using System.Collections.Generic;
using RelatorioGLPIApp.ViewModels;

namespace RelatorioGLPIApp.Models
{
    public class SavedReportState
    {
        public DateTimeOffset ReportDate { get; set; }
        public string TechnicianUsername { get; set; } = string.Empty;
        public List<RelatorioItem> Items { get; set; } = new();
    }
}