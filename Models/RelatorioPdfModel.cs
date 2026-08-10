using System;
using System.Collections.Generic;
using RelatorioGLPIApp.ViewModels;

namespace RelatorioGLPIApp.Models;

public class RelatorioPdfModel
{
    public string NomeTecnico { get; }
    public DateTimeOffset DataRelatorio { get; }
    public List<RelatorioItem> Itens { get; }
    public int TotalItens { get; }
    public int TotalSolucionados { get; }
    public int TotalChamadosAbertos { get; }

    public RelatorioPdfModel(string nomeTecnico, DateTimeOffset dataRelatorio, List<RelatorioItem> itens, int totalItens, int totalSolucionados, int totalChamadosAbertos)
    {
        NomeTecnico = string.IsNullOrWhiteSpace(nomeTecnico)
            ? "Técnico"
            : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(nomeTecnico.Replace('.', ' '));
        DataRelatorio = dataRelatorio;
        Itens = itens;
        TotalItens = totalItens;
        TotalSolucionados = totalSolucionados;
        TotalChamadosAbertos = totalChamadosAbertos;
    }
}
