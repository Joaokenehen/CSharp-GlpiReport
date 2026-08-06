namespace RelatorioGLPIApp.Services;

public interface ILogService
{
    void Info(string contexto, string mensagem);
    void Sucesso(string contexto, string mensagem);
    void Erro(string contexto, string mensagem);
}