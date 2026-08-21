using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using Xceed.Words.NET;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Xceed.Document.NET;
using System.IO;
using RelatorioGLPIApp.Models;
using MessageBox.Avalonia.Enums;

namespace RelatorioGLPIApp.ViewModels;

public enum TechnicianSortColumn
{
    Name,
    SolvedCount,
    ResolutionRate,
    OnDutySolvedCount,
    AverageSolveTime
}

public partial class GeneralReportsViewModel : ViewModelBase, IOnDutyChecker
{
    private readonly ILogService _log;
    private readonly GlpiConnectionInfo _connectionInfo;
    private readonly IChamadoService _chamadoService;
    private readonly IGeneralReportStateService _generalReportStateService;

    // Propriedade para acessar os comandos do Dashboard (Sair, Relatórios Salvos)
    public DashboardViewModel DashboardContext { get; }

    [ObservableProperty]
    private DateTimeOffset _startDate = new(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeOffset.Now.Offset);

    [ObservableProperty]
    private DateTimeOffset _endDate = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _generationStatus = "Pronto para gerar relatório.";

    // Propriedades para as estatísticas (ainda não calculadas)
    [ObservableProperty]
    private int _totalTicketsFound;
    [ObservableProperty]
    private int _totalSolved;
    [ObservableProperty]
    private string _taxaResolucaoDia = "N/A";
    [ObservableProperty]
    private bool _isResolutionRateVisible;
    [ObservableProperty]
    private string _averageTicketsPerDay = "N/A";
    [ObservableProperty]
    private bool _isAverageVisible;
    [ObservableProperty]
    private string _averageSolveTime = "N/A";
    [ObservableProperty]
    private int _totalBusinessHours;
    [ObservableProperty]
    private int _totalOnDuty;
    [ObservableProperty]
    private int _totalPending;
    [ObservableProperty]
    private int _totalNew;

    [ObservableProperty]
    private string _matrizPercentage = "";

    [ObservableProperty]
    private string _agenciasPercentage = "";

    [ObservableProperty]
    private string _filiaisPercentage = "";

    [ObservableProperty]
    private string _reportSaveName = "";

    [ObservableProperty]
    private ObservableCollection<string> _savedGeneralReports = new();

    [ObservableProperty]
    private ObservableCollection<DepartmentStat> _matrizStats = new();

    [ObservableProperty]
    private ObservableCollection<DepartmentStat> _agenciasStats = new();

    [ObservableProperty]
    private ObservableCollection<DepartmentStat> _filiaisStats = new();

    [ObservableProperty]
    private ObservableCollection<DepartmentStat> _garagemStats = new();
    [ObservableProperty]
    private string _garagemPercentage = "";

    [ObservableProperty]
    private ObservableCollection<DepartmentStat> _encomendasStats = new();
    [ObservableProperty]
    private string _encomendasPercentage = "";

    [ObservableProperty]
    private ObservableCollection<DepartmentStat> _agenciasPropriasStats = new();
    [ObservableProperty]
    private string _agenciasPropriasPercentage = "";

    private List<Chamado> _currentReportTickets = new();

    [ObservableProperty]
    private ObservableCollection<TechnicianStat> _technicianStats = new();

    [ObservableProperty]
    private TechnicianSortColumn _currentSortColumn = TechnicianSortColumn.SolvedCount;

    [ObservableProperty]
    private bool _isSortAscending = false;

    public Action<string, List<Chamado>, bool>? OnShowTechnicianDetailRequested { get; set; }
    public Action? OnBackToDashboardRequested { get; set; }
    public Action? OnNavigateToTechnicianReportsRequested { get; set; }

    public GeneralReportsViewModel(GlpiConnectionInfo connectionInfo, ILogService logService, DashboardViewModel dashboardContext)
    {
        _log = logService;
        DashboardContext = dashboardContext;
        _connectionInfo = connectionInfo;
        _chamadoService = connectionInfo.ChamadoService;
        _generalReportStateService = new GeneralReportStateService();

    }

    [RelayCommand]
    private async Task GenerateReport()
    {
        _log.Info("RelatorioGeral", "Iniciando geração de relatório para o dia de hoje.");
        IsGenerating = true;
        GenerationStatus = "Buscando chamados no GLPI...";

        // Reseta as estatísticas
        TotalTicketsFound = 0;
        TotalSolved = 0;
        TotalBusinessHours = 0;
        TotalOnDuty = 0;
        TotalPending = 0;
        TotalNew = 0;
        MatrizStats.Clear();
        MatrizPercentage = "";
        AgenciasStats.Clear();
        AgenciasPercentage = "";
        FiliaisStats.Clear();
        FiliaisPercentage = "";
        GaragemStats.Clear();
        GaragemPercentage = "";
        EncomendasStats.Clear();
        EncomendasPercentage = "";
        AgenciasPropriasStats.Clear();
        AgenciasPropriasPercentage = "";
        AverageTicketsPerDay = "N/A";
        IsAverageVisible = false;
        TechnicianStats.Clear();
        AverageSolveTime = "N/A";
        IsResolutionRateVisible = false;
        ReportSaveName = "";

        try
        {
            // MUDANÇA: Para o relatório do dia, usamos o método rápido que busca os chamados
            // mais recentes (igual ao dashboard), em vez de paginar por toda a base de dados.
            // A filtragem para o dia de hoje é feita localmente no método ProcessTickets.
            var allTickets = await _chamadoService.ObterChamadosAsync(
                _connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken);

            GenerationStatus = $"Processando {allTickets.Count} chamados...";
            await Task.Delay(100); // Permite que a UI atualize a mensagem

            if (allTickets.Any())
            {
                _currentReportTickets = ProcessTickets(allTickets, true);
                GenerationStatus = $"Relatório gerado com sucesso. {TotalTicketsFound} chamados encontrados no período.";
            }
            else
            {
                GenerationStatus = "Nenhum chamado encontrado no GLPI.";
            }
        }
        catch (Exception ex)
        {
            _log.Erro("RelatorioGeral", $"Falha ao gerar relatório: {ex.Message}");
            GenerationStatus = "Ocorreu um erro ao gerar o relatório.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private List<Chamado> ProcessTickets(List<Chamado> allTickets, bool filterForToday)
    {
        _log.Info("RelatorioGeral", $"Iniciando processamento. Filtro de hoje: {filterForToday}. Total de chamados: {allTickets.Count}.");

        List<Chamado> ticketsToProcess;

        if (filterForToday)
        {
            // Define o período do relatório para HOJE.
            var reportStartDate = DateTime.Today;
            var reportEndDate = DateTime.Today.AddDays(1);

            // 1. FILTRAGEM: Seleciona apenas os chamados criados HOJE.
            ticketsToProcess = allTickets.Where(t =>
            {
                if (DateTime.TryParse(t.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataCriacao))
                {
                    return dataCriacao >= reportStartDate && dataCriacao < reportEndDate;
                }
                return false;
            }).ToList();
            _log.Info("RelatorioGeral", $"{ticketsToProcess.Count} chamados encontrados criados hoje.");
        }
        else
        {
            ticketsToProcess = allTickets;
            _log.Info("RelatorioGeral", $"Processando todos os {allTickets.Count} chamados (sem filtro de data).");
        }

        // 2. PROCESSAMENTO: Calcula as estatísticas a partir da lista filtrada (ou completa).
        TotalTicketsFound = ticketsToProcess.Count;

        // Reseta as métricas condicionais
        IsResolutionRateVisible = filterForToday;
        IsAverageVisible = !filterForToday;

        int onDutyCount = 0;
        int solvedCount = 0;
        int pendingCount = 0;
        int newCount = 0;
        var solveDurationsInSeconds = new List<long>();
        int resolvedTodayCount = 0;

        foreach (var ticket in ticketsToProcess)
        {
            // Contagem de status
            if (ticket.Status == 5 || ticket.Status == 6) // Solucionado ou Fechado (criado no período)
            {
                solvedCount++;

                // Usa o tempo de resolução calculado pelo GLPI, que já desconsidera o tempo pendente.
                if (ticket.TempoParaSolucao.HasValue && ticket.TempoParaSolucao > 0)
                {
                    solveDurationsInSeconds.Add(ticket.TempoParaSolucao.Value);
                }
            }
            else if (ticket.Status == 4)
            {
                pendingCount++; // Conta apenas os pendentes que foram abertos hoje.
            }
            else if (ticket.Status == 1)
            {
                newCount++; // Novo
            }

            bool isTicketOnDuty = IsTicketOnDuty(ticket, filterForToday);
            if (isTicketOnDuty)
            {
                onDutyCount++;
            }

            // Contagem para Taxa de Resolução: Chamados abertos hoje E resolvidos/fechados hoje
            if ((ticket.Status == 5 || ticket.Status == 6) &&
                filterForToday && ((DateTime.TryParse(ticket.DataSolucao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataSolucaoLocal) && dataSolucaoLocal.Date == DateTime.Today) ||
                 (DateTime.TryParse(ticket.DataFechamento, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataFechamentoLocal) && dataFechamentoLocal.Date == DateTime.Today)))
            {
                resolvedTodayCount++;
            }
        }

        TotalSolved = solvedCount;
        TotalBusinessHours = TotalTicketsFound - onDutyCount; // Chamados no período - chamados de plantão no período
        TotalOnDuty = onDutyCount;
        TotalPending = pendingCount;
        TotalNew = newCount;

        // Calcula a Taxa de Resolução
        if (filterForToday)
        {
            if (TotalTicketsFound > 0)
            {
                TaxaResolucaoDia = $"{(double)resolvedTodayCount / TotalTicketsFound:P0}";
            }
            else
            {
                TaxaResolucaoDia = "N/A";
            }
        }
        else
        {
            TaxaResolucaoDia = "N/A"; // Não aplicável para o relatório completo
        }

        // Calcula a Média de Chamados por Dia para o relatório completo
        if (!filterForToday && ticketsToProcess.Any())
        {
            var creationDates = ticketsToProcess
                .Select(t => DateTime.TryParse(t.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt) ? (DateTime?)dt : null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value.Date)
                .ToList();

            if (creationDates.Any())
            {
                var minDate = creationDates.Min();
                var maxDate = creationDates.Max();
                // Adiciona 1 para incluir o dia de início e o de fim no período.
                var totalDays = (maxDate - minDate).TotalDays + 1;

                if (totalDays > 0)
                {
                    var average = (double)TotalTicketsFound / totalDays;
                    AverageTicketsPerDay = $"{average:F1}";
                }
            }
        }

        // Calcula o Tempo Médio de Resolução
        if (solveDurationsInSeconds.Any())
        {
            var averageSeconds = solveDurationsInSeconds.Average();
            var averageTimeSpan = TimeSpan.FromSeconds(averageSeconds);
            AverageSolveTime = FormatTimeSpan(averageTimeSpan);
        }
        else
        {
            AverageSolveTime = "N/A";
        }

        _log.Info("RelatorioGeral", "Estatísticas calculadas:");
        _log.Info("RelatorioGeral", $"  - Abertos no Período: {TotalTicketsFound}");
        _log.Info("RelatorioGeral", $"  - Solucionados/Fechados: {TotalSolved}");
        _log.Info("RelatorioGeral", $"  - Em Expediente Normal: {TotalBusinessHours}");
        _log.Info("RelatorioGeral", $"  - Taxa de Resolução: {TaxaResolucaoDia}");
        _log.Info("RelatorioGeral", $"  - Em Plantão: {TotalOnDuty}");
        _log.Info("RelatorioGeral", $"  - Pendentes (do Período): {TotalPending}");
        _log.Info("RelatorioGeral", $"  - Tempo Médio de Resolução: {AverageSolveTime}");
        _log.Info("RelatorioGeral", $"  - Média Diária: {AverageTicketsPerDay}");
        _log.Info("RelatorioGeral", $"  - Novos: {TotalNew}");

        // 4. CÁLCULO POR TÉCNICO
        _log.Info("RelatorioGeral", "Calculando produtividade por técnico...");
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

            string resolutionRate = (TotalTicketsFound > 0) ? $"{(double)techSolvedCount / TotalTicketsFound:P0}" : "0%";

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
                System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(techName.Replace('.', ' ')), // FormattedTechnicianName
                techSolvedCount,
                resolutionRate,
                onDutySolvedCount,
                avgSolveTime
            ));
        }
        _log.Info("RelatorioGeral", $"{TechnicianStats.Count} técnicos encontrados e analisados.");

        // Aplica a ordenação padrão
        SortTechnicians(TechnicianSortColumn.SolvedCount.ToString());

        // 3. CÁLCULO POR SETOR
        _log.Info("RelatorioGeral", "Calculando estatísticas por setor...");

        // Clear previous stats
        MatrizStats.Clear();
        AgenciasStats.Clear();
        FiliaisStats.Clear();
        GaragemStats.Clear();
        EncomendasStats.Clear();
        AgenciasPropriasStats.Clear();

        var ticketsMatriz = new List<Chamado>();
        var ticketsAgencias = new List<Chamado>();
        var ticketsFiliais = new List<Chamado>();
        var ticketsGaragem = new List<Chamado>();
        var ticketsEncomendas = new List<Chamado>();
        var ticketsAgenciasProprias = new List<Chamado>();

        foreach (var ticket in ticketsToProcess)
        {
            string entidade = WebUtility.HtmlDecode(ticket.Entidade ?? "Matriz").Trim();

            // Prioridade na classificação: do mais específico para o mais geral
            if (entidade.Contains("Agências Próprias", StringComparison.OrdinalIgnoreCase) || entidade.Contains("Agencias Proprias", StringComparison.OrdinalIgnoreCase))
            {
                ticketsAgenciasProprias.Add(ticket);
            }
            else if (entidade.Contains("Encomendas", StringComparison.OrdinalIgnoreCase))
            {
                ticketsEncomendas.Add(ticket);
            }
            else if (entidade.Contains("Garagem", StringComparison.OrdinalIgnoreCase))
            {
                ticketsGaragem.Add(ticket);
            }
            else if (entidade.Contains("Agências", StringComparison.OrdinalIgnoreCase) || entidade.Contains("Agencias", StringComparison.OrdinalIgnoreCase))
            {
                ticketsAgencias.Add(ticket);
            }
            // Verifica se a entidade contém "Filiais"
            else if (entidade.Contains("Filiais", StringComparison.OrdinalIgnoreCase))
            {
                ticketsFiliais.Add(ticket);
            }
            else
            {
                ticketsMatriz.Add(ticket);
            }
        }

        // Helper function to process a group of tickets and populate the stats collection
        void ProcessDepartmentGroup(List<Chamado> groupTickets, ObservableCollection<DepartmentStat> collection) { var stats = groupTickets.Select(t => { string setor = WebUtility.HtmlDecode(t.Entidade ?? "Matriz").Split('>').Last().Trim(); if (setor.Equals("Setor", StringComparison.OrdinalIgnoreCase)) return "Arrecadação"; return setor; }).GroupBy(setor => setor).Select(g => new DepartmentStat(g.Key, g.Count())).OrderByDescending(s => s.TicketCount).ToList(); collection.Clear(); foreach (var stat in stats) collection.Add(stat); }

        ProcessDepartmentGroup(ticketsMatriz, MatrizStats);
        ProcessDepartmentGroup(ticketsAgencias, AgenciasStats);
        ProcessDepartmentGroup(ticketsFiliais, FiliaisStats);
        ProcessDepartmentGroup(ticketsGaragem, GaragemStats);
        ProcessDepartmentGroup(ticketsEncomendas, EncomendasStats);
        ProcessDepartmentGroup(ticketsAgenciasProprias, AgenciasPropriasStats);

        if (TotalTicketsFound > 0)
        {
            double matrizCount = MatrizStats.Sum(s => s.TicketCount);
            double agenciasCount = AgenciasStats.Sum(s => s.TicketCount);
            double filiaisCount = FiliaisStats.Sum(s => s.TicketCount);
            double garagemCount = GaragemStats.Sum(s => s.TicketCount);
            double encomendasCount = EncomendasStats.Sum(s => s.TicketCount);
            double agenciasPropriasCount = AgenciasPropriasStats.Sum(s => s.TicketCount);

            MatrizPercentage = $"({(matrizCount / TotalTicketsFound):P0})";
            AgenciasPercentage = $"({(agenciasCount / TotalTicketsFound):P0})";
            FiliaisPercentage = $"({(filiaisCount / TotalTicketsFound):P0})";
            GaragemPercentage = $"({(garagemCount / TotalTicketsFound):P0})";
            EncomendasPercentage = $"({(encomendasCount / TotalTicketsFound):P0})";
            AgenciasPropriasPercentage = $"({(agenciasPropriasCount / TotalTicketsFound):P0})";
        }

        _log.Info("RelatorioGeral", $"Matriz: {MatrizStats.Count} setores {MatrizPercentage}. " +
                                   $"Agências: {AgenciasStats.Count} setores {AgenciasPercentage}. " +
                                   $"Filiais: {FiliaisStats.Count} setores {FiliaisPercentage}. " +
                                   $"Garagem: {GaragemStats.Count} setores {GaragemPercentage}. " +
                                   $"Encomendas: {EncomendasStats.Count} setores {EncomendasPercentage}. " +
                                   $"Ag. Próprias: {AgenciasPropriasStats.Count} setores {AgenciasPropriasPercentage}.");

        return ticketsToProcess;
    }

    [RelayCommand]
    private async Task GenerateFullReport()
    {
        _log.Info("RelatorioGeral", "Iniciando geração de relatório COMPLETO.");
        IsGenerating = true;
        GenerationStatus = "Buscando todos os chamados no GLPI (pode levar um tempo)...";

        // Reseta as estatísticas
        TotalTicketsFound = 0;
        TotalSolved = 0;
        TotalBusinessHours = 0;
        TotalOnDuty = 0;
        TotalPending = 0;
        TotalNew = 0;
        MatrizStats.Clear();
        MatrizPercentage = "";
        AgenciasStats.Clear();
        AgenciasPercentage = "";
        FiliaisStats.Clear();
        FiliaisPercentage = "";
        GaragemStats.Clear();
        GaragemPercentage = "";
        EncomendasStats.Clear();
        EncomendasPercentage = "";
        AgenciasPropriasStats.Clear();
        AgenciasPropriasPercentage = "";
        AverageTicketsPerDay = "N/A";
        IsAverageVisible = false;
        TechnicianStats.Clear();
        AverageSolveTime = "N/A";
        IsResolutionRateVisible = false;
        ReportSaveName = "";

        try
        {
            var allTickets = await _chamadoService.ObterChamadosParaRelatorioGeralAsync(_connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken, default, default);
            GenerationStatus = $"Processando todos os {allTickets.Count} chamados...";
            await Task.Delay(100);
            _currentReportTickets = ProcessTickets(allTickets, false);
            GenerationStatus = $"Relatório completo gerado com sucesso. {TotalTicketsFound} chamados encontrados.";
        }
        catch (Exception ex)
        {
            _log.Erro("RelatorioGeral", $"Falha ao gerar relatório completo: {ex.Message}");
            GenerationStatus = "Ocorreu um erro ao gerar o relatório completo.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task SaveState()
    {
        if (string.IsNullOrWhiteSpace(ReportSaveName))
        {
            // Poderíamos mostrar uma notificação aqui se quiséssemos.
            return;
        }

        bool shouldSave = true;
        if (await _generalReportStateService.ReportExists(ReportSaveName))
        {
            var result = await MessageBoxManager.GetMessageBoxStandard(
                "Arquivo Existente",
                $"Um relatório geral com o nome '{ReportSaveName}' já existe.\nDeseja substituí-lo?",
                ButtonEnum.YesNo, Icon.Warning).ShowAsync();
            shouldSave = result == ButtonResult.Yes;
        }

        if (shouldSave)
        {
            var state = new SavedGeneralReportState
            {
                TotalTicketsFound = this.TotalTicketsFound,
                TotalSolved = this.TotalSolved,
                TaxaResolucaoDia = this.TaxaResolucaoDia,
                TotalBusinessHours = this.TotalBusinessHours,
                GaragemStats = this.GaragemStats.ToList(),
                EncomendasStats = this.EncomendasStats.ToList(),
                AgenciasPropriasStats = this.AgenciasPropriasStats.ToList(),
                GaragemPercentage = this.GaragemPercentage,
                EncomendasPercentage = this.EncomendasPercentage,
                AgenciasPropriasPercentage = this.AgenciasPropriasPercentage,
                TechnicianStats = this.TechnicianStats.ToList(),
                AverageSolveTime = this.AverageSolveTime,
                AverageTicketsPerDay = this.AverageTicketsPerDay,
                TotalOnDuty = this.TotalOnDuty,
                TotalPending = this.TotalPending,
                TotalNew = this.TotalNew,
                MatrizPercentage = this.MatrizPercentage,
                AgenciasPercentage = this.AgenciasPercentage,
                FiliaisPercentage = this.FiliaisPercentage,
                MatrizStats = this.MatrizStats.ToList(),
                AgenciasStats = this.AgenciasStats.ToList(),
                FiliaisStats = this.FiliaisStats.ToList()
            };

            await _generalReportStateService.SaveState(state, ReportSaveName);
            await DashboardContext.RefreshAllSavedReports();
            ReportSaveName = "";
        }
    }

    [RelayCommand]
    private async Task LoadState(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return;

        var state = await _generalReportStateService.LoadState(reportId);
        if (state != null)
        {
            TotalTicketsFound = state.TotalTicketsFound;
            TotalSolved = state.TotalSolved;
            TaxaResolucaoDia = state.TaxaResolucaoDia;
            TotalBusinessHours = state.TotalBusinessHours;
            GaragemStats.Clear();
            foreach (var item in state.GaragemStats) GaragemStats.Add(item);
            EncomendasStats.Clear();
            foreach (var item in state.EncomendasStats) EncomendasStats.Add(item);
            AgenciasPropriasStats.Clear();
            foreach (var item in state.AgenciasPropriasStats) AgenciasPropriasStats.Add(item);
            TechnicianStats.Clear();
            foreach (var item in state.TechnicianStats) TechnicianStats.Add(item);
            AverageSolveTime = state.AverageSolveTime;
            AverageTicketsPerDay = state.AverageTicketsPerDay;
            TotalOnDuty = state.TotalOnDuty;
            TotalPending = state.TotalPending;
            TotalNew = state.TotalNew;
            MatrizPercentage = state.MatrizPercentage;
            AgenciasPercentage = state.AgenciasPercentage;
            GaragemPercentage = state.GaragemPercentage;
            EncomendasPercentage = state.EncomendasPercentage;
            AgenciasPropriasPercentage = state.AgenciasPropriasPercentage;
            FiliaisPercentage = state.FiliaisPercentage;

            MatrizStats.Clear();
            foreach (var item in state.MatrizStats) MatrizStats.Add(item);
            AgenciasStats.Clear();
            foreach (var item in state.AgenciasStats) AgenciasStats.Add(item);
            FiliaisStats.Clear();
            foreach (var item in state.FiliaisStats) FiliaisStats.Add(item);

            GenerationStatus = $"Relatório '{Path.GetFileNameWithoutExtension(reportId)}' carregado.";

            // Ajusta a visibilidade das métricas com base nos dados carregados
            IsResolutionRateVisible = TaxaResolucaoDia != "N/A";
            IsAverageVisible = AverageTicketsPerDay != "N/A";
        }
    }

    [RelayCommand]
    private async Task DeleteSavedReport(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return;

        var result = await MessageBoxManager.GetMessageBoxStandard(
            "Excluir Relatório Geral",
            $"Tem certeza que deseja excluir o relatório '{reportId}'?",
            ButtonEnum.YesNo, Icon.Warning).ShowAsync();

        if (result == ButtonResult.Yes)
        {
            await _generalReportStateService.DeleteState(reportId);
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
        _log.Info("Imprimir", "Iniciando processo de impressão do relatório geral.");

        try
        {
            // 1. Gera o PDF em memória
            var model = new GeneralReportPdfModel(
                TotalTicketsFound, TotalSolved, TotalBusinessHours, TotalOnDuty, TaxaResolucaoDia, TotalPending, TotalNew, AverageTicketsPerDay, AverageSolveTime,
                MatrizPercentage, AgenciasPercentage, FiliaisPercentage,
                GaragemPercentage, EncomendasPercentage, AgenciasPropriasPercentage,
                MatrizStats, AgenciasStats, FiliaisStats,
                GaragemStats, EncomendasStats, AgenciasPropriasStats, TechnicianStats);
            var document = new Documents.GeneralReportPdfDocument(model);
            byte[] pdfBytes = document.GeneratePdf();

            // 2. Salva o PDF em um arquivo temporário
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"RelatorioGeral_{Guid.NewGuid()}.pdf");
            await File.WriteAllBytesAsync(tempFilePath, pdfBytes);
            _log.Info("Imprimir", $"Relatório salvo temporariamente em: {tempFilePath}");

            // 3. Envia o arquivo para a impressora padrão do sistema (funciona melhor no Windows)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = tempFilePath,
                    Verb = "Print",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true // IMPORTANTE: Usa o shell do SO para interpretar o verbo "Print"
                }
            };
            process.Start();
            _log.Sucesso("Imprimir", "Arquivo enviado para a fila de impressão.");
        }
        catch (Win32Exception winEx) when (winEx.NativeErrorCode == 1155) // ERROR_NO_ASSOCIATION
        {
            _log.Erro("Imprimir", $"Falha de associação de arquivo (Código 1155): {winEx.Message}");
            var box = MessageBoxManager.GetMessageBoxStandard("Associação de Arquivo Faltando",
                "O Windows não sabe como imprimir arquivos PDF.\n\n" +
                "Para corrigir, por favor, instale um leitor de PDF (como o Adobe Acrobat Reader) e defina-o como o programa padrão para abrir arquivos .pdf.",
                ButtonEnum.Ok, Icon.Warning);
            await box.ShowAsync();
        }
        catch (Exception ex)
        {
            _log.Erro("Imprimir", $"Falha ao enviar para a impressora: {ex.Message}");
            var box = MessageBoxManager.GetMessageBoxStandard("Erro de Impressão", $"Não foi possível enviar o relatório para a impressora.\nVerifique se você tem um leitor de PDF padrão configurado.\n\nErro: {ex.Message}", ButtonEnum.Ok, Icon.Error);
            await box.ShowAsync();
        }
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        OnBackToDashboardRequested?.Invoke();
    }

    [RelayCommand]
    private void NavigateToTechnicianReports()
    {
        OnNavigateToTechnicianReportsRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ExportarPdf()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            _log.Erro("Exportar", "Não foi possível obter a janela principal para abrir o diálogo de salvamento.");
            return;
        }

        var suggestedFileName = $"Relatorio_Geral_{DateTime.Now:yyyy_MM_dd}.pdf";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório Geral em PDF",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[] { new FilePickerFileType("Arquivos PDF") { Patterns = new[] { "*.pdf" } } }
        });

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                var model = new GeneralReportPdfModel(
                    TotalTicketsFound, TotalSolved, TotalBusinessHours, TotalOnDuty, TaxaResolucaoDia, TotalPending, TotalNew, AverageTicketsPerDay, AverageSolveTime,
                    MatrizPercentage, AgenciasPercentage, FiliaisPercentage,
                    GaragemPercentage, EncomendasPercentage, AgenciasPropriasPercentage,
                    MatrizStats, AgenciasStats, FiliaisStats,
                    GaragemStats, EncomendasStats, AgenciasPropriasStats,
                    TechnicianStats);
                var document = new Documents.GeneralReportPdfDocument(model);
                document.GeneratePdf(stream);
                _log.Sucesso("Exportar", $"Relatório Geral salvo com sucesso em: {file.Name}");
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
        if (topLevel == null)
        {
            _log.Erro("Exportar", "Não foi possível obter a janela principal para abrir o diálogo de salvamento.");
            return;
        }

        var suggestedFileName = $"Relatorio_Geral_{DateTime.Now:yyyy_MM_dd}.docx";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório Geral em Word",
            SuggestedFileName = suggestedFileName,
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
                _log.Sucesso("Exportar", $"Relatório Geral salvo com sucesso em: {file.Name}");
            }
            catch (Exception ex)
            {
                _log.Erro("Exportar Word", $"Ocorreu um erro ao gerar o documento Word: {ex.Message}");
            }
        }
    }

    private void GerarConteudoWord(DocX document)
    {
        document.InsertParagraph("Relatório Geral de Chamados").Bold().FontSize(20).Alignment = Alignment.center;

        document.InsertParagraph("Resumo do Período").Bold().FontSize(14);

        var stats = new List<KeyValuePair<string, string>>
        {
            new("Chamados Abertos no Período:", TotalTicketsFound.ToString()),
            new("Chamados Solucionados/Fechados:", TotalSolved.ToString()),
            new("Chamados em Expediente Normal:", TotalBusinessHours.ToString()),
            new("Chamados em Horário de Plantão:", TotalOnDuty.ToString())
        };

        if (IsResolutionRateVisible) stats.Add(new("Taxa de Resolução:", TaxaResolucaoDia));
        if (IsAverageVisible) stats.Add(new("Média Diária de Chamados:", AverageTicketsPerDay));
        if (AverageSolveTime != "N/A") stats.Add(new("Tempo Médio de Resolução:", AverageSolveTime));

        stats.Add(new("Chamados Pendentes:", TotalPending.ToString()));
        stats.Add(new("Chamados Novos:", TotalNew.ToString()));

        var statsTable = document.AddTable(stats.Count, 2);
        statsTable.Design = TableDesign.TableGrid;
        statsTable.AutoFit = AutoFit.Contents;
        for (int i = 0; i < stats.Count; i++)
        {
            statsTable.Rows[i].Cells[0].Paragraphs.First().Append(stats[i].Key);
            statsTable.Rows[i].Cells[1].Paragraphs.First().Append(stats[i].Value).Bold();
        }
        document.InsertTable(statsTable);
        document.InsertParagraph();

        void CreateDepartmentTable(string title, string percentage, ICollection<DepartmentStat> stats)
        {
            if (!stats.Any()) return;
            document.InsertParagraph($"{title} {percentage}").Bold().FontSize(14);
            var table = document.AddTable(stats.Count + 1, 2);
            table.Design = TableDesign.TableGrid;
            table.Rows[0].Cells[0].Paragraphs.First().Append("Setor").Bold();
            table.Rows[0].Cells[1].Paragraphs.First().Append("Chamados").Bold();
            int rowIndex = 1;
            foreach (var stat in stats) { table.Rows[rowIndex].Cells[0].Paragraphs.First().Append(stat.DepartmentName); table.Rows[rowIndex++].Cells[1].Paragraphs.First().Append(stat.TicketCount.ToString()); }
            document.InsertTable(table);
            document.InsertParagraph();
        }

        CreateDepartmentTable("Demandas da Matriz", MatrizPercentage, MatrizStats);
        CreateDepartmentTable("Demandas das Agências", AgenciasPercentage, AgenciasStats);
        CreateDepartmentTable("Demandas das Filiais", FiliaisPercentage, FiliaisStats);
        CreateDepartmentTable("Demandas da Garagem", GaragemPercentage, GaragemStats);
        CreateDepartmentTable("Demandas de Encomendas", EncomendasPercentage, EncomendasStats);
        CreateDepartmentTable("Demandas de Agências Próprias", AgenciasPropriasPercentage, AgenciasPropriasStats);

        if (TechnicianStats.Any())
        {
            document.InsertParagraph("Produtividade por Técnico").Bold().FontSize(14);
            var techTable = document.AddTable(TechnicianStats.Count + 1, 5);
            techTable.Design = TableDesign.TableGrid;
            techTable.Rows[0].Cells[0].Paragraphs.First().Append("Técnico").Bold();
            techTable.Rows[0].Cells[1].Paragraphs.First().Append("Solucionados").Bold();
            techTable.Rows[0].Cells[2].Paragraphs.First().Append("Taxa Res.").Bold();
            techTable.Rows[0].Cells[3].Paragraphs.First().Append("Plantão").Bold();
            techTable.Rows[0].Cells[4].Paragraphs.First().Append("T. Médio").Bold();
            int techRowIndex = 1; // Start from 1 because row 0 is header
            foreach (var stat in TechnicianStats) { techTable.Rows[techRowIndex].Cells[0].Paragraphs.First().Append(stat.FormattedTechnicianName); techTable.Rows[techRowIndex].Cells[1].Paragraphs.First().Append(stat.SolvedCount.ToString()); techTable.Rows[techRowIndex].Cells[2].Paragraphs.First().Append(stat.ResolutionRate); techTable.Rows[techRowIndex].Cells[3].Paragraphs.First().Append(stat.OnDutySolvedCount.ToString()); techTable.Rows[techRowIndex++].Cells[4].Paragraphs.First().Append(stat.AverageSolveTime); }
            document.InsertTable(techTable);
            document.InsertParagraph();
        }
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
        // Helper para parse de data, para ser usado na lógica de plantão
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
    private void ShowTechnicianDetail(TechnicianStat? technician)
    {
        if (technician == null)
        {
            _log.Erro("Navigation", "Comando ShowTechnicianDetail foi chamado com um técnico nulo.");
            return;
        }
        _log.Info("Navigation", $"Comando ShowTechnicianDetail executado para o técnico: {technician.RawTechnicianName}. Disparando evento...");

        // A flag 'IsResolutionRateVisible' nos diz se o relatório atual é o do dia ou o completo.
        bool isDailyReport = IsResolutionRateVisible;
        OnShowTechnicianDetailRequested?.Invoke(technician.RawTechnicianName, _currentReportTickets, isDailyReport);
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

public class GrupoDeChamados
{
    public string Filial { get; set; }

    public int Quantidade => Chamados?.Count ?? 0;

    public ObservableCollection<Chamado> Chamados { get; set; }

    public ObservableCollection<GrupoDeChamados> ChamadosAgrupados { get; set; } = new();


}

public record DepartmentStat(string DepartmentName, int TicketCount);

public record TechnicianStat(
    string RawTechnicianName,
    string FormattedTechnicianName,
    int SolvedCount,
    string ResolutionRate,
    int OnDutySolvedCount,
    string AverageSolveTime
);