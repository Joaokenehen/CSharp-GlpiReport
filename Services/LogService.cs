using System;

namespace RelatorioGLPIApp.Services;

public class LogService : ILogService
{
    public void Info(string contexto, string mensagem)
    {
        Console.ResetColor();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] [{contexto}] {mensagem}");
    }

    public void Sucesso(string contexto, string mensagem)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SUCESSO] [{contexto}] {mensagem}");
        Console.ResetColor();
    }

    public void Erro(string contexto, string mensagem)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERRO] [{contexto}] {mensagem}");
        Console.ResetColor();
    }
}