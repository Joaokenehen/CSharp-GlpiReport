using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.ViewModels;

public partial class GeneralReportsViewModel : ViewModelBase
{
    private readonly ILogService _log;
    private readonly GlpiConnectionInfo _connectionInfo;
    private readonly IChamadoService _chamadoService;

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
    private int _totalOnDuty;
    [ObservableProperty]
    private int _totalPending;
    [ObservableProperty]
    private int _totalNew;

    public Action? OnBackToDashboardRequested { get; set; }

    public GeneralReportsViewModel(GlpiConnectionInfo connectionInfo, ILogService logService, DashboardViewModel dashboardContext)
    {
        _log = logService;
        DashboardContext = dashboardContext;
        _connectionInfo = connectionInfo;
        _chamadoService = connectionInfo.ChamadoService;
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
        TotalOnDuty = 0;
        TotalPending = 0;
        TotalNew = 0;

        try
        {
            // O serviço busca todos os chamados, o filtro será local.
            var allTickets = await _chamadoService.ObterChamadosParaRelatorioGeralAsync(
                _connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken, default, default);

            GenerationStatus = $"Processando {allTickets.Count} chamados...";
            await Task.Delay(100); // Permite que a UI atualize a mensagem

            if (allTickets.Any())
            {
                ProcessTickets(allTickets);
                GenerationStatus = $"Relatório gerado com sucesso. {TotalTicketsFound} chamados abertos hoje encontrados.";
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

    private void ProcessTickets(List<Chamado> allTickets)
    {
        _log.Info("RelatorioGeral", $"Iniciando processamento e filtragem local de {allTickets.Count} chamados.");

        // Define o período do relatório para HOJE.
        var reportStartDate = DateTime.Today;
        var reportEndDate = DateTime.Today.AddDays(1);

        // 1. FILTRAGEM: Seleciona apenas os chamados criados HOJE.
        var ticketsNoPeriodo = allTickets.Where(t =>
        {
            if (DateTime.TryParse(t.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataCriacao))
            {
                return dataCriacao >= reportStartDate && dataCriacao < reportEndDate;
            }
            return false;
        }).ToList();

        _log.Info("RelatorioGeral", $"{ticketsNoPeriodo.Count} chamados encontrados criados hoje.");

        // 2. PROCESSAMENTO: Calcula as estatísticas a partir da lista já filtrada (chamados de HOJE).
        TotalTicketsFound = ticketsNoPeriodo.Count;

        int onDutyCount = 0;
        int solvedCount = 0;
        int pendingCount = 0;
        int newCount = 0;

        foreach (var ticket in ticketsNoPeriodo)
        {
            // Contagem de status
            if (ticket.Status == 5 || ticket.Status == 6) solvedCount++; // Solucionado ou Fechado
            else if (ticket.Status == 4)
            {
                pendingCount++; // Conta apenas os pendentes que foram abertos hoje.
            }
            else if (ticket.Status == 1) newCount++; // Novo

            // Contagem de chamados de plantão (lógica simplificada baseada na data de criação)
            if (DateTime.TryParse(ticket.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataCriacaoLocal))
            {
                var time = dataCriacaoLocal.TimeOfDay;
                var dayOfWeek = dataCriacaoLocal.DayOfWeek;

                bool isNightShift = time >= TimeSpan.FromHours(18) || time < TimeSpan.FromHours(7.5);
                bool isLunchShift = time >= TimeSpan.FromHours(11.5) && time < TimeSpan.FromHours(13.5);
                bool isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;

                if (isNightShift || isLunchShift || isWeekend)
                {
                    onDutyCount++;
                }
            }
        }

        TotalSolved = solvedCount;
        TotalOnDuty = onDutyCount;
        TotalPending = pendingCount;
        TotalNew = newCount;

        _log.Info("RelatorioGeral", "Estatísticas calculadas:");
        _log.Info("RelatorioGeral", $"  - Abertos Hoje: {TotalTicketsFound}");
        _log.Info("RelatorioGeral", $"  - Solucionados: {TotalSolved}");
        _log.Info("RelatorioGeral", $"  - Em Plantão: {TotalOnDuty}");
        _log.Info("RelatorioGeral", $"  - Pendentes (do dia): {TotalPending}");
        _log.Info("RelatorioGeral", $"  - Novos: {TotalNew}");
    }

    [RelayCommand]
    private async Task LoadStateAndGoToDashboard(string? reportId)
    {
        // Este comando permite carregar um relatório salvo a partir desta tela
        if (DashboardContext?.LoadStateCommand.CanExecute(reportId) ?? false)
        {
            await DashboardContext.LoadStateCommand.ExecuteAsync(reportId);
            OnBackToDashboardRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        OnBackToDashboardRequested?.Invoke();
    }
}