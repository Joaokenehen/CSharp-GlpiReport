using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using QuestPDF.Fluent;
using Xceed.Document.NET;
using Xceed.Words.NET;
using MessageBox.Avalonia.Enums;

namespace RelatorioGLPIApp.ViewModels;

public partial class TechnicianReportsViewModel : ViewModelBase, IOnDutyChecker
{
    private readonly ILogService _log;
    private readonly GlpiConnectionInfo _connectionInfo;
    private readonly IChamadoService _chamadoService;
    private readonly ITechnicianReportStateService _technicianReportStateService;
    private readonly bool _isOfflineMode;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _generationStatus = "";

    [ObservableProperty]
    private ObservableCollection<TechnicianStat> _technicianStats = new();

    [ObservableProperty]
    private string _reportSaveName = "";

    [ObservableProperty]
    private ObservableCollection<string> _savedTechnicianReports = new();

    [ObservableProperty]
    private TechnicianSortColumn _currentSortColumn = TechnicianSortColumn.SolvedCount;

    [ObservableProperty]
    private bool _isSortAscending = false;

    public DashboardViewModel DashboardContext { get; }

    private List<Chamado> _currentReportTickets = new();
    private bool _isDailyReport;

    public Action? OnBackToDashboardRequested { get; set; }
    public Action? OnNavigateToGeneralReportsRequested { get; set; }
    public Action<string, List<Chamado>, bool>? OnShowTechnicianDetailRequested { get; set; }

    public TechnicianReportsViewModel(GlpiConnectionInfo connectionInfo, ILogService logService, DashboardViewModel dashboardContext)
    {
        _log = logService;
        _connectionInfo = connectionInfo;
        _chamadoService = connectionInfo.ChamadoService;
        _technicianReportStateService = new TechnicianReportStateService();
        DashboardContext = dashboardContext;
        _isOfflineMode = connectionInfo.IsOffline;

    }

    [RelayCommand]
    private async Task GenerateReport()
    {

        if (_isOfflineMode)
        {
            GenerationStatus = "Você está no modo offline. Conecte-se ao GLPI para gerar relatórios de técnicos.";
            return;
        }

        _log.Info("RelatorioTecnicos", "Iniciando geração de relatório do dia para técnicos.");
        IsGenerating = true;
        GenerationStatus = "Buscando chamados no GLPI...";
        TechnicianStats.Clear();

        try
        {
            var allTickets = await _chamadoService.ObterChamadosAsync(_connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken);
            GenerationStatus = $"Processando {allTickets.Count} chamados...";
            await Task.Delay(100);

            if (allTickets.Any())
            {
                _isDailyReport = true;
                _currentReportTickets = ProcessTechnicianTickets(allTickets, true);
                GenerationStatus = $"Relatório do dia gerado com sucesso. {TechnicianStats.Count} técnicos analisados.";
            }
            else
            {
                GenerationStatus = "Nenhum chamado encontrado no GLPI.";
            }
        }
        catch (Exception ex)
        {
            _log.Erro("RelatorioTecnicos", $"Falha ao gerar relatório: {ex.Message}");
            GenerationStatus = "Ocorreu um erro ao gerar o relatório.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateFullReport()
    {

        if (_isOfflineMode)
        {
            GenerationStatus = "Você está no modo offline. Conecte-se ao GLPI para gerar o relatório completo.";
            return;
        }

        _log.Info("RelatorioTecnicos", "Iniciando geração de relatório COMPLETO para técnicos.");
        IsGenerating = true;
        GenerationStatus = "Buscando todos os chamados no GLPI (pode levar um tempo)...";
        TechnicianStats.Clear();

        try
        {
            var allTickets = await _chamadoService.ObterChamadosParaRelatorioGeralAsync(_connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken, default, default);
            GenerationStatus = $"Processando todos os {allTickets.Count} chamados...";
            await Task.Delay(100);

            _isDailyReport = false;
            _currentReportTickets = ProcessTechnicianTickets(allTickets, false);
            GenerationStatus = $"Relatório completo gerado com sucesso. {TechnicianStats.Count} técnicos analisados.";
        }
        catch (Exception ex)
        {
            _log.Erro("RelatorioTecnicos", $"Falha ao gerar relatório completo: {ex.Message}");
            GenerationStatus = "Ocorreu um erro ao gerar o relatório completo.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // Este método é uma cópia adaptada do GeneralReportsViewModel.ProcessTickets
    // para garantir que a lógica de cálculo dos técnicos seja idêntica.
    private List<Chamado> ProcessTechnicianTickets(List<Chamado> allTickets, bool filterForToday)
    {
        _log.Info("RelatorioTecnicos", $"Iniciando processamento. Filtro de hoje: {filterForToday}. Total de chamados: {allTickets.Count}.");

        List<Chamado> ticketsToProcess;

        if (filterForToday)
        {
            var reportStartDate = DateTime.Today;
            var reportEndDate = DateTime.Today.AddDays(1);
            ticketsToProcess = allTickets.Where(t =>
            {
                if (DateTime.TryParse(t.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataCriacao))
                {
                    return dataCriacao >= reportStartDate && dataCriacao < reportEndDate;
                }
                return false;
            }).ToList();
            _log.Info("RelatorioTecnicos", $"{ticketsToProcess.Count} chamados encontrados criados hoje.");
        }
        else
        {
            ticketsToProcess = allTickets;
            _log.Info("RelatorioTecnicos", $"Processando todos os {allTickets.Count} chamados (sem filtro de data).");
        }

        int totalTicketsFoundInPeriod = ticketsToProcess.Count;

        // CÁLCULO POR TÉCNICO
        _log.Info("RelatorioTecnicos", "Calculando produtividade por técnico...");
        TechnicianStats.Clear();

        var allTechnicians = ticketsToProcess
            .Where(t => !string.IsNullOrWhiteSpace(t.TecnicoAtribuido))
            .SelectMany(t => t.TecnicoAtribuido!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        foreach (var techName in allTechnicians)
        {
            var techTickets = ticketsToProcess
                .Where(t => t.TecnicoAtribuido != null && t.TecnicoAtribuido.Contains(techName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var solvedByTech = techTickets.Where(t => t.Status == 5 || t.Status == 6).ToList();
            int techSolvedCount = solvedByTech.Count;

            // A taxa de resolução é baseada no total de chamados do período processado.
            string resolutionRate = (totalTicketsFoundInPeriod > 0) ? $"{(double)techSolvedCount / totalTicketsFoundInPeriod:P0}" : "0%";

            int onDutySolvedCount = solvedByTech.Count(t => IsTicketOnDuty(t, filterForToday));

            var techSolveDurations = solvedByTech
                .Where(t => t.TempoParaSolucao.HasValue && t.TempoParaSolucao > 0)
                .Select(t => t.TempoParaSolucao!.Value)
                .ToList();

            string avgSolveTime = "N/A";
            if (techSolveDurations.Any())
            {
                var averageSeconds = techSolveDurations.Average();
                avgSolveTime = FormatTimeSpan(TimeSpan.FromSeconds(averageSeconds));
            }

            TechnicianStats.Add(new TechnicianStat(
                techName, // RawTechnicianName
                CultureInfo.CurrentCulture.TextInfo.ToTitleCase(techName.Replace('.', ' ')), // FormattedTechnicianName
                techSolvedCount,
                resolutionRate,
                onDutySolvedCount,
                avgSolveTime
            ));
        }
        _log.Info("RelatorioTecnicos", $"{TechnicianStats.Count} técnicos encontrados e analisados.");

        // Aplica a ordenação padrão
        SortTechnicians(TechnicianSortColumn.SolvedCount.ToString());

        return ticketsToProcess;
    }

    [RelayCommand]
    private async Task SaveState()
    {
        if (string.IsNullOrWhiteSpace(ReportSaveName)) return;

        bool shouldSave = true;
        if (await _technicianReportStateService.ReportExists(ReportSaveName))
        {
            var result = await MessageBoxManager.GetMessageBoxStandard(
                "Arquivo Existente",
                $"Um relatório de técnico com o nome '{ReportSaveName}' já existe.\nDeseja substituí-lo?",
                ButtonEnum.YesNo, Icon.Warning).ShowAsync();
            shouldSave = result == ButtonResult.Yes;
        }

        if (shouldSave)
        {
            var state = new SavedTechnicianReportState
            {
                TechnicianStats = this.TechnicianStats.ToList(),
                IsDailyReport = _isDailyReport
            };

            await _technicianReportStateService.SaveState(state, ReportSaveName);
            await DashboardContext.RefreshAllSavedReports();
            ReportSaveName = "";
        }
    }

    [RelayCommand]
    private async Task LoadState(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return;

        var state = await _technicianReportStateService.LoadState(reportId);
        if (state != null)
        {
            TechnicianStats.Clear();
            foreach (var item in state.TechnicianStats) TechnicianStats.Add(item);
            _isDailyReport = state.IsDailyReport;

            GenerationStatus = $"Relatório de técnico '{Path.GetFileNameWithoutExtension(reportId)}' carregado.";
        }
    }

    [RelayCommand]
    private async Task DeleteSavedReport(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return;

        var result = await MessageBoxManager.GetMessageBoxStandard(
            "Excluir Relatório de Técnico",
            $"Tem certeza que deseja excluir o relatório '{reportId}'?",
            ButtonEnum.YesNo, Icon.Warning).ShowAsync();

        if (result == ButtonResult.Yes)
        {
            await _technicianReportStateService.DeleteState(reportId);
            await DashboardContext.RefreshAllSavedReports();
        }
    }

    [RelayCommand]
    private void UseSavedReportName(string? reportFileName)
    {
        if (!string.IsNullOrWhiteSpace(reportFileName))
            ReportSaveName = Path.GetFileNameWithoutExtension(reportFileName);
    }

    [RelayCommand]
    private async Task PrintReport()
    {
        _log.Info("Imprimir", "Iniciando processo de impressão do relatório de técnicos.");
        try
        {
            var model = new TechnicianReportPdfModel(TechnicianStats);
            var document = new Documents.TechnicianReportPdfDocument(model);
            byte[] pdfBytes = document.GeneratePdf();

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"RelatorioTecnicos_{Guid.NewGuid()}.pdf");
            await File.WriteAllBytesAsync(tempFilePath, pdfBytes);

            var process = new Process { StartInfo = new ProcessStartInfo { FileName = tempFilePath, Verb = "Print", CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = true } };
            process.Start();
            _log.Sucesso("Imprimir", "Arquivo enviado para a fila de impressão.");
        }
        catch (Win32Exception winEx) when (winEx.NativeErrorCode == 1155)
        {
            _log.Erro("Imprimir", $"Falha de associação de arquivo (Código 1155): {winEx.Message}");
            await MessageBoxManager.GetMessageBoxStandard("Associação de Arquivo Faltando", "O Windows não sabe como imprimir arquivos PDF.\n\nPara corrigir, por favor, instale um leitor de PDF (como o Adobe Acrobat Reader) e defina-o como o programa padrão para abrir arquivos .pdf.", ButtonEnum.Ok, Icon.Warning).ShowAsync();
        }
        catch (Exception ex)
        {
            _log.Erro("Imprimir", $"Falha ao enviar para a impressora: {ex.Message}");
            await MessageBoxManager.GetMessageBoxStandard("Erro de Impressão", $"Não foi possível enviar o relatório para a impressora.\nVerifique se você tem um leitor de PDF padrão configurado.\n\nErro: {ex.Message}", ButtonEnum.Ok, Icon.Error).ShowAsync();
        }
    }

    [RelayCommand]
    private async Task ExportarPdf()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório de Técnicos em PDF",
            SuggestedFileName = $"Relatorio_Tecnicos_{DateTime.Now:yyyy_MM_dd}.pdf",
            FileTypeChoices = new[] { new FilePickerFileType("Arquivos PDF") { Patterns = new[] { "*.pdf" } } }
        });

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                var model = new TechnicianReportPdfModel(TechnicianStats);
                var document = new Documents.TechnicianReportPdfDocument(model);
                document.GeneratePdf(stream);
                _log.Sucesso("Exportar", $"Relatório de Técnicos salvo com sucesso em: {file.Name}");
            }
            catch (Exception ex)
            {
                _log.Erro("Exportar PDF", $"Ocorreu um erro ao gerar o PDF: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportarWord()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório de Técnicos em Word",
            SuggestedFileName = $"Relatorio_Tecnicos_{DateTime.Now:yyyy_MM_dd}.docx",
            FileTypeChoices = new[] { new FilePickerFileType("Documentos do Word") { Patterns = new[] { "*.docx" } } }
        });

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                using (var document = DocX.Create(stream))
                {
                    GerarConteudoWord(document);
                    document.Save();
                }
                _log.Sucesso("Exportar", $"Relatório de Técnicos salvo com sucesso em: {file.Name}");
            }
            catch (Exception ex)
            {
                _log.Erro("Exportar Word", $"Ocorreu um erro ao gerar o documento Word: {ex.Message}");
            }
        }
    }

    private void GerarConteudoWord(DocX document)
    {
        document.InsertParagraph("Relatório de Produtividade de Técnicos").Bold().FontSize(20).Alignment = Alignment.center;
        document.InsertParagraph();

        if (TechnicianStats.Any())
        {
            var techTable = document.AddTable(TechnicianStats.Count + 1, 5);
            techTable.Design = TableDesign.TableGrid;
            techTable.Rows[0].Cells[0].Paragraphs.First().Append("Técnico").Bold();
            techTable.Rows[0].Cells[1].Paragraphs.First().Append("Solucionados").Bold();
            techTable.Rows[0].Cells[2].Paragraphs.First().Append("Taxa Res.").Bold();
            techTable.Rows[0].Cells[3].Paragraphs.First().Append("Plantão").Bold();
            techTable.Rows[0].Cells[4].Paragraphs.First().Append("T. Médio").Bold();
            int techRowIndex = 1;
            foreach (var stat in TechnicianStats) { techTable.Rows[techRowIndex].Cells[0].Paragraphs.First().Append(stat.FormattedTechnicianName); techTable.Rows[techRowIndex].Cells[1].Paragraphs.First().Append(stat.SolvedCount.ToString()); techTable.Rows[techRowIndex].Cells[2].Paragraphs.First().Append(stat.ResolutionRate); techTable.Rows[techRowIndex].Cells[3].Paragraphs.First().Append(stat.OnDutySolvedCount.ToString()); techTable.Rows[techRowIndex++].Cells[4].Paragraphs.First().Append(stat.AverageSolveTime); }
            document.InsertTable(techTable);
        }
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        OnBackToDashboardRequested?.Invoke();
    }

    [RelayCommand]
    private void NavigateToGeneralReports()
    {
        OnNavigateToGeneralReportsRequested?.Invoke();
    }

    [RelayCommand]
    private void ShowTechnicianDetail(TechnicianStat? technician)
    {
        if (technician == null)
        {
            _log.Erro("Navigation", "Comando ShowTechnicianDetail foi chamado com um técnico nulo.");
            return;
        }
        _log.Info("Navigation", $"Comando ShowTechnicianDetail executado para o técnico: {technician.RawTechnicianName}. Disparando evento...");
        OnShowTechnicianDetailRequested?.Invoke(technician.RawTechnicianName, _currentReportTickets, _isDailyReport);
    }

    private Avalonia.Controls.TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private string FormatTimeSpan(TimeSpan ts)
    {
        var parts = new List<string>();
        if (ts.Days > 0) parts.Add($"{ts.Days}d");
        if (ts.Hours > 0) parts.Add($"{ts.Hours}h");
        if (ts.Minutes > 0) parts.Add($"{ts.Minutes}m");
        if (!parts.Any()) return "< 1 min";
        return string.Join(" ", parts);
    }

    public bool IsTicketOnDuty(Chamado ticket, bool filterForToday)
    {
        DateTime? ParseDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)) return dt.ToUniversalTime();
            return null;
        }

        if (filterForToday)
        {
            var dataCriacao = ParseDate(ticket.DataCriacao);
            var dataSolucao = ParseDate(ticket.DataSolucao);
            var dataModificacao = ParseDate(ticket.DataModificacao);
            var dataAtribuicao = ParseDate(ticket.DataAtribuicao);

            var diaSelecionadoLocal = DateTime.Today;
            DateTimeOffset inicioPlantaoLocal;
            if (diaSelecionadoLocal.DayOfWeek == DayOfWeek.Monday)
                inicioPlantaoLocal = diaSelecionadoLocal.AddDays(-3).AddHours(18);
            else
                inicioPlantaoLocal = diaSelecionadoLocal.AddDays(-1).AddHours(18);

            var fimPlantaoLocal = diaSelecionadoLocal.AddHours(7).AddMinutes(30);
            var inicioAlmocoPlantaoLocal = diaSelecionadoLocal.AddHours(11).AddMinutes(30);
            var fimAlmocoPlantaoLocal = diaSelecionadoLocal.AddHours(13).AddMinutes(30);

            var inicioPlantaoUtc = inicioPlantaoLocal.ToUniversalTime();
            var fimPlantaoUtc = fimPlantaoLocal.ToUniversalTime();
            var inicioAlmocoPlantaoUtc = inicioAlmocoPlantaoLocal.ToUniversalTime();
            var fimAlmocoPlantaoUtc = fimAlmocoPlantaoLocal.ToUniversalTime();

            bool IsInOnDutyWindow(DateTime? date)
            {
                if (!date.HasValue) return false;
                return (date.Value >= inicioPlantaoUtc && date.Value < fimPlantaoUtc) || (date.Value >= inicioAlmocoPlantaoUtc && date.Value < fimAlmocoPlantaoUtc);
            }

            return IsInOnDutyWindow(dataCriacao) || IsInOnDutyWindow(dataSolucao) || IsInOnDutyWindow(dataModificacao) || IsInOnDutyWindow(dataAtribuicao);
        }
        else if (DateTime.TryParse(ticket.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataCriacaoLocal))
        {
            var time = dataCriacaoLocal.TimeOfDay;
            var dayOfWeek = dataCriacaoLocal.DayOfWeek;
            bool isNightShift = time >= TimeSpan.FromHours(18) || time < TimeSpan.FromHours(7.5);
            bool isLunchShift = time >= TimeSpan.FromHours(11.5) && time < TimeSpan.FromHours(13.5);
            bool isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
            return isNightShift || isLunchShift || isWeekend;
        }
        return false;
    }

    [RelayCommand]
    private void SortTechnicians(string? column)
    {
        if (!Enum.TryParse<TechnicianSortColumn>(column, true, out var sortBy))
        {
            return;
        }

        if (sortBy == CurrentSortColumn)
        {
            IsSortAscending = !IsSortAscending;
        }
        else
        {
            CurrentSortColumn = sortBy;
            IsSortAscending = false; // Padrão: decrescente para nova coluna
        }

        var statsToSort = TechnicianStats.ToList();
        IEnumerable<TechnicianStat> sortedStats;

        switch (CurrentSortColumn)
        {
            case TechnicianSortColumn.SolvedCount:
                sortedStats = IsSortAscending ? statsToSort.OrderBy(s => s.SolvedCount) : statsToSort.OrderByDescending(s => s.SolvedCount);
                break;
            case TechnicianSortColumn.ResolutionRate:
                sortedStats = IsSortAscending ? statsToSort.OrderBy(s => double.Parse(s.ResolutionRate.TrimEnd('%'))) : statsToSort.OrderByDescending(s => double.Parse(s.ResolutionRate.TrimEnd('%')));
                break;
            case TechnicianSortColumn.OnDutySolvedCount:
                sortedStats = IsSortAscending ? statsToSort.OrderBy(s => s.OnDutySolvedCount) : statsToSort.OrderByDescending(s => s.OnDutySolvedCount);
                break;
            case TechnicianSortColumn.AverageSolveTime:
                sortedStats = IsSortAscending ? statsToSort.OrderBy(s => ParseAverageSolveTime(s.AverageSolveTime)) : statsToSort.OrderByDescending(s => ParseAverageSolveTime(s.AverageSolveTime));
                break;
            default:
                sortedStats = IsSortAscending ? statsToSort.OrderBy(s => s.FormattedTechnicianName) : statsToSort.OrderByDescending(s => s.FormattedTechnicianName);
                break;
        }

        TechnicianStats.Clear();
        foreach (var stat in sortedStats)
        {
            TechnicianStats.Add(stat);
        }
    }

    private TimeSpan ParseAverageSolveTime(string timeStr)
    {
        if (timeStr == "N/A") return TimeSpan.MaxValue;
        if (timeStr == "< 1 min") return TimeSpan.FromSeconds(30);

        var totalTime = TimeSpan.Zero;
        var parts = timeStr.Split(' ');
        foreach (var part in parts)
        {
            if (part.EndsWith("d")) totalTime = totalTime.Add(TimeSpan.FromDays(double.Parse(part.TrimEnd('d'))));
            else if (part.EndsWith("h")) totalTime = totalTime.Add(TimeSpan.FromHours(double.Parse(part.TrimEnd('h'))));
            else if (part.EndsWith("m")) totalTime = totalTime.Add(TimeSpan.FromMinutes(double.Parse(part.TrimEnd('m'))));
        }
        return totalTime;
    }
}