using RelatorioGLPIApp.ViewModels;
using System.Collections.Generic;

namespace RelatorioGLPIApp.Models;

public record GeneralReportPdfModel(
    int TotalTicketsFound,
    int TotalSolved,
    int TotalBusinessHours,
    int TotalOnDuty,
    string TaxaResolucaoDia,
    int TotalPending,
    int TotalNew,
    string AverageTicketsPerDay,
    string AverageSolveTime,
    string MatrizPercentage,
    string AgenciasPercentage,
    string FiliaisPercentage,
    string GaragemPercentage,
    string EncomendasPercentage,
    string AgenciasPropriasPercentage,
    ICollection<DepartmentStat> MatrizStats,
    ICollection<DepartmentStat> AgenciasStats,
    ICollection<DepartmentStat> FiliaisStats,
    ICollection<DepartmentStat> GaragemStats,
    ICollection<DepartmentStat> EncomendasStats, // This was already correct
    ICollection<DepartmentStat> AgenciasPropriasStats, // This was already correct
    ICollection<TechnicianStat> TechnicianStats
);