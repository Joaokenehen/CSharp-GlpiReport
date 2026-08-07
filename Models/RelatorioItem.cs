using CommunityToolkit.Mvvm.ComponentModel;

namespace RelatorioGLPIApp.ViewModels;

public partial class RelatorioItem : ObservableObject
{
    [ObservableProperty]
    private string _categoria = "Suporte Matriz";

    [ObservableProperty]
    private string _titulo = string.Empty;

    [ObservableProperty]
    private string _descricao = string.Empty;

    [ObservableProperty]
    private bool _isOrigemGlpi;
}

