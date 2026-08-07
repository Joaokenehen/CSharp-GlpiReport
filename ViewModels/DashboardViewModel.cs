using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<RelatorioItem> _relatorios;

    public DashboardViewModel(List<Chamado> chamadosDoGlpi)
    {
        Relatorios = new ObservableCollection<RelatorioItem>();

        foreach (var chamado in chamadosDoGlpi)
        {
            Relatorios.Add(new RelatorioItem
            {
                Categoria = "Suporte Matriz",
                Titulo = $"~Chamado: {chamado.Id} – {chamado.Titulo}~",
                Descricao = chamado.Descricao,
                IsOrigemGlpi = true
            });
        }
    }

    [RelayCommand]
    private void AdicionarItemManual()
    {
        Relatorios.Add(new RelatorioItem
        {
            Categoria = "Suporte Matriz",
            Titulo = "~Nova Atividade~",
            Descricao = "Descrição do que foi feito.",
            IsOrigemGlpi = false
        });
    }

    [RelayCommand]
    private void RemoverItem(RelatorioItem itemParaRemover)
    {
        if (itemParaRemover != null && Relatorios.Contains(itemParaRemover))
        {
            Relatorios.Remove(itemParaRemover);
        }
    }
}