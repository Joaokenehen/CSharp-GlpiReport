using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelatorioGLPIApp.Services;
using RelatorioGLPIApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RelatorioGLPIApp.ViewModels;

public partial class TechnicianDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _technicianName;

    [ObservableProperty]
    private ObservableCollection<TechnicianDetailTicket> _solvedTickets = new();

    public Action? OnBackToGeneralReportsRequested { get; set; }

    private readonly IOnDutyChecker _onDutyChecker;

    public TechnicianDetailViewModel(string technicianName, List<Chamado> allProcessedTickets, bool filterForToday, IOnDutyChecker onDutyChecker)
    {
        _technicianName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(technicianName.Replace('.', ' '));
        _onDutyChecker = onDutyChecker;
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
            SolvedTickets.Add(new TechnicianDetailTicket(ticket.Id, ticket.Titulo, ticketType));
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        OnBackToGeneralReportsRequested?.Invoke();
    }
}