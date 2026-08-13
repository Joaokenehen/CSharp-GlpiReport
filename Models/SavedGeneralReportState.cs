using System.Collections.Generic;
using RelatorioGLPIApp.ViewModels;

namespace RelatorioGLPIApp.Models;

public class SavedGeneralReportState
{
    // All the stats
    public int TotalTicketsFound { get; set; }
    public int TotalSolved { get; set; }
    public string TaxaResolucaoDia { get; set; } = "N/A";
    public int TotalBusinessHours { get; set; }
    public string AverageSolveTime { get; set; } = "N/A";
    public string AverageTicketsPerDay { get; set; } = "N/A";
    public int TotalOnDuty { get; set; }
    public int TotalPending { get; set; }
    public int TotalNew { get; set; }

    // Percentages
    public string MatrizPercentage { get; set; } = "";
    public string AgenciasPercentage { get; set; } = "";
    public string FiliaisPercentage { get; set; } = "";
    public string GaragemPercentage { get; set; } = "";
    public string EncomendasPercentage { get; set; } = "";
    public string AgenciasPropriasPercentage { get; set; } = "";

    // Department stats
    public List<DepartmentStat> MatrizStats { get; set; } = new();
    public List<DepartmentStat> AgenciasStats { get; set; } = new();
    public List<DepartmentStat> FiliaisStats { get; set; } = new();
    public List<DepartmentStat> GaragemStats { get; set; } = new();
    public List<DepartmentStat> EncomendasStats { get; set; } = new();
    public List<DepartmentStat> AgenciasPropriasStats { get; set; } = new();
    public List<TechnicianStat> TechnicianStats { get; set; } = new();
}