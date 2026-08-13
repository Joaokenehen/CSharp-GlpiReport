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

public partial class GeneralReportsViewModel : ViewModelBase
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

    public Action? OnBackToDashboardRequested { get; set; }

    public GeneralReportsViewModel(GlpiConnectionInfo connectionInfo, ILogService logService, DashboardViewModel dashboardContext)
    {
        _log = logService;
        DashboardContext = dashboardContext;
        _connectionInfo = connectionInfo;
        _chamadoService = connectionInfo.ChamadoService;
        _generalReportStateService = new GeneralReportStateService();

        _ = LoadSavedReportsList();
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
                ProcessTickets(allTickets, true);
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

    private void ProcessTickets(List<Chamado> allTickets, bool filterForToday)
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

        int onDutyCount = 0;
        int solvedCount = 0;
        int pendingCount = 0;
        int newCount = 0;
        int resolvedTodayCount = 0;

        // Helper para parse de data, para ser usado na lógica de plantão
        DateTime? ParseDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)) return dt.ToUniversalTime();
            return null;
        }

        foreach (var ticket in ticketsToProcess)
        {
            // Contagem de status
            if (ticket.Status == 5 || ticket.Status == 6) // Solucionado ou Fechado (criado no período)
            {
                solvedCount++;
            }
            else if (ticket.Status == 4)
            {
                pendingCount++; // Conta apenas os pendentes que foram abertos hoje.
            }
            else if (ticket.Status == 1)
            {
                newCount++; // Novo
            }

            bool isTicketOnDuty = false;
            if (filterForToday)
            {
                // LÓGICA ROBUSTA PARA O RELATÓRIO DO DIA (igual ao Dashboard)
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
                    return (date.Value >= inicioPlantaoUtc && date.Value < fimPlantaoUtc) ||
                           (date.Value >= inicioAlmocoPlantaoUtc && date.Value < fimAlmocoPlantaoUtc);
                }

                if (IsInOnDutyWindow(dataCriacao) || IsInOnDutyWindow(dataSolucao) || IsInOnDutyWindow(dataModificacao) || IsInOnDutyWindow(dataAtribuicao))
                {
                    isTicketOnDuty = true;
                }
            }
            else
            {
                // LÓGICA SIMPLIFICADA PARA O RELATÓRIO COMPLETO (baseado na data de criação)
                if (DateTime.TryParse(ticket.DataCriacao, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dataCriacaoLocal))
                {
                    var time = dataCriacaoLocal.TimeOfDay;
                    var dayOfWeek = dataCriacaoLocal.DayOfWeek;

                    bool isNightShift = time >= TimeSpan.FromHours(18) || time < TimeSpan.FromHours(7.5);
                    bool isLunchShift = time >= TimeSpan.FromHours(11.5) && time < TimeSpan.FromHours(13.5);
                    bool isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;

                    if (isNightShift || isLunchShift || isWeekend)
                    {
                        isTicketOnDuty = true;
                    }
                }
            }

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

        _log.Info("RelatorioGeral", "Estatísticas calculadas:");
        _log.Info("RelatorioGeral", $"  - Abertos no Período: {TotalTicketsFound}");
        _log.Info("RelatorioGeral", $"  - Solucionados/Fechados: {TotalSolved}");
        _log.Info("RelatorioGeral", $"  - Em Expediente Normal: {TotalBusinessHours}");
        _log.Info("RelatorioGeral", $"  - Taxa de Resolução: {TaxaResolucaoDia}");
        _log.Info("RelatorioGeral", $"  - Em Plantão: {TotalOnDuty}");
        _log.Info("RelatorioGeral", $"  - Pendentes (do Período): {TotalPending}");
        _log.Info("RelatorioGeral", $"  - Novos: {TotalNew}");

        // 3. CÁLCULO POR SETOR
        _log.Info("RelatorioGeral", "Calculando estatísticas por setor...");

        var ticketsMatriz = new List<Chamado>();
        var ticketsAgencias = new List<Chamado>();
        var ticketsFiliais = new List<Chamado>();

        foreach (var ticket in ticketsToProcess)
        {
            string entidade = WebUtility.HtmlDecode(ticket.Entidade ?? "Matriz");
            if (entidade.Contains("Agências", StringComparison.OrdinalIgnoreCase) || entidade.Contains("Agencias", StringComparison.OrdinalIgnoreCase))
            {
                ticketsAgencias.Add(ticket);
            }
            else if (entidade.Contains("Filiais", StringComparison.OrdinalIgnoreCase))
            {
                ticketsFiliais.Add(ticket);
            }
            else
            {
                ticketsMatriz.Add(ticket);
            }
        }

        // Função para processar um grupo de tickets e popular a coleção de estatísticas
        void ProcessGroup(List<Chamado> groupTickets, ObservableCollection<DepartmentStat> collection)
        {
            var stats = groupTickets
                .Select(t =>
                {
                    // Extrai o nome do setor da entidade
                    string setor = WebUtility.HtmlDecode(t.Entidade ?? "Matriz").Split('>').Last().Trim();
                    // REGRA DE NEGÓCIO: Se o setor for "Setor", renomeia para "Arrecadação".
                    if (setor.Equals("Setor", StringComparison.OrdinalIgnoreCase)) return "Arrecadação";
                    return setor;
                })
                .GroupBy(setor => setor)
                .Select(g => new DepartmentStat(g.Key, g.Count()))
                .OrderByDescending(s => s.TicketCount)
                .ToList();

            foreach (var stat in stats) collection.Add(stat);
        }

        ProcessGroup(ticketsMatriz, MatrizStats);
        ProcessGroup(ticketsAgencias, AgenciasStats);
        ProcessGroup(ticketsFiliais, FiliaisStats);

        if (TotalTicketsFound > 0)
        {
            double matrizCount = MatrizStats.Sum(s => s.TicketCount);
            double agenciasCount = AgenciasStats.Sum(s => s.TicketCount);
            double filiaisCount = FiliaisStats.Sum(s => s.TicketCount);

            MatrizPercentage = $"({(matrizCount / TotalTicketsFound):P0})";
            AgenciasPercentage = $"({(agenciasCount / TotalTicketsFound):P0})";
            FiliaisPercentage = $"({(filiaisCount / TotalTicketsFound):P0})";
        }

        _log.Info("RelatorioGeral", $"Matriz: {MatrizStats.Count} setores {MatrizPercentage}. " +
                                   $"Agências: {AgenciasStats.Count} setores {AgenciasPercentage}. " +
                                   $"Filiais: {FiliaisStats.Count} setores {FiliaisPercentage}.");
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
        ReportSaveName = "";

        try
        {
            var allTickets = await _chamadoService.ObterChamadosParaRelatorioGeralAsync(_connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken, default, default);
            GenerationStatus = $"Processando todos os {allTickets.Count} chamados...";
            await Task.Delay(100);
            ProcessTickets(allTickets, false);
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
            await LoadSavedReportsList();
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
            TotalOnDuty = state.TotalOnDuty;
            TotalPending = state.TotalPending;
            TotalNew = state.TotalNew;
            MatrizPercentage = state.MatrizPercentage;
            AgenciasPercentage = state.AgenciasPercentage;
            FiliaisPercentage = state.FiliaisPercentage;

            MatrizStats.Clear();
            foreach (var item in state.MatrizStats) MatrizStats.Add(item);
            AgenciasStats.Clear();
            foreach (var item in state.AgenciasStats) AgenciasStats.Add(item);
            FiliaisStats.Clear();
            foreach (var item in state.FiliaisStats) FiliaisStats.Add(item);

            GenerationStatus = $"Relatório '{Path.GetFileNameWithoutExtension(reportId)}' carregado.";
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
            await LoadSavedReportsList();
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
                TotalTicketsFound, TotalSolved, TotalBusinessHours, TotalOnDuty, TaxaResolucaoDia, TotalPending, TotalNew,
                MatrizPercentage, AgenciasPercentage, FiliaisPercentage,
                MatrizStats, AgenciasStats, FiliaisStats);
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
                    TotalTicketsFound, TotalSolved, TotalBusinessHours, TotalOnDuty, TaxaResolucaoDia, TotalPending, TotalNew,
                    MatrizPercentage, AgenciasPercentage, FiliaisPercentage,
                    MatrizStats, AgenciasStats, FiliaisStats);
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
        document.InsertParagraph($"Dados referentes ao dia: {DateTime.Now:dd/MM/yyyy}").FontSize(12).Alignment = Alignment.center;
        document.InsertParagraph();

        document.InsertParagraph("Resumo do Período").Bold().FontSize(14);
        var statsTable = document.AddTable(7, 2);
        statsTable.Design = TableDesign.TableGrid;
        statsTable.AutoFit = AutoFit.Contents;
        statsTable.Rows[0].Cells[0].Paragraphs.First().Append("Chamados Abertos no Período:").Append(TotalTicketsFound.ToString()).Bold();
        statsTable.Rows[1].Cells[0].Paragraphs.First().Append("Chamados Solucionados/Fechados:").Append(TotalSolved.ToString()).Bold();
        statsTable.Rows[2].Cells[0].Paragraphs.First().Append("Chamados em Expediente Normal:").Append(TotalBusinessHours.ToString()).Bold();
        statsTable.Rows[3].Cells[0].Paragraphs.First().Append("Chamados em Horário de Plantão:").Append(TotalOnDuty.ToString()).Bold();
        statsTable.Rows[4].Cells[0].Paragraphs.First().Append("Taxa de Resolução:").Append(TaxaResolucaoDia).Bold();
        statsTable.Rows[5].Cells[0].Paragraphs.First().Append("Chamados Pendentes:").Append(TotalPending.ToString()).Bold();
        statsTable.Rows[6].Cells[0].Paragraphs.First().Append("Chamados Novos:").Append(TotalNew.ToString()).Bold();
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
    }

    private Avalonia.Controls.TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private async Task LoadSavedReportsList()
    {
        var reports = await _generalReportStateService.GetSavedReportIds();
        SavedGeneralReports.Clear();
        foreach (var report in reports.OrderByDescending(r => r))
            SavedGeneralReports.Add(report);
    }
}

public record DepartmentStat(string DepartmentName, int TicketCount);