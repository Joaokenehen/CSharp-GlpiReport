using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Services;
using RelatorioGLPIApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace RelatorioGLPIApp.ViewModels;

public partial class TechnicianDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _technicianName;

    [ObservableProperty]
    private ObservableCollection<TechnicianDetailTicket> _solvedTickets = new();

    public Action? OnBackToGeneralReportsRequested { get; set; }

    private readonly GlpiConnectionInfo _connectionInfo;
    private readonly IOnDutyChecker _onDutyChecker;

    public TechnicianDetailViewModel(string technicianName, List<Chamado> allProcessedTickets, bool filterForToday, IOnDutyChecker onDutyChecker, GlpiConnectionInfo connectionInfo)
    {
        _technicianName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(technicianName.Replace('.', ' '));
        _onDutyChecker = onDutyChecker;
        _connectionInfo = connectionInfo;
        LoadTickets(technicianName, allProcessedTickets, filterForToday);
    }

    private void LoadTickets(string techName, List<Chamado> allProcessedTickets, bool filterForToday)
    {
        var techTickets = allProcessedTickets
            .Where(t => (t.Status == 5 || t.Status == 6) &&
                        t.TecnicoAtribuido != null &&
                        t.TecnicoAtribuido.Contains(techName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var ticket in techTickets)
        {
            string ticketType = _onDutyChecker.IsTicketOnDuty(ticket, filterForToday) ? "Plantão" : "Normal";
            SolvedTickets.Add(new TechnicianDetailTicket(ticket.Id, ticket.Titulo ?? "Título não encontrado", ticketType));
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        OnBackToGeneralReportsRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ToggleConversation(TechnicianDetailTicket? ticket)
    {
        if (ticket == null) return;

        ticket.IsExpanded = !ticket.IsExpanded;

        // Only load the conversation if it's being expanded and hasn't been loaded yet.
        if (ticket.IsExpanded && ticket.Conversation.Count == 0)
        {
            ticket.IsLoadingConversation = true;
            var followups = await _connectionInfo.ChamadoService.GetTicketFollowupsAsync(_connectionInfo.Url, _connectionInfo.AppToken, _connectionInfo.SessionToken, ticket.Id);
            foreach (var followup in followups)
            {
                ticket.Conversation.Add(followup);
            }
            ticket.IsLoadingConversation = false;
        }
    }
}