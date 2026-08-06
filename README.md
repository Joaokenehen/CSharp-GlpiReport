# 📊 Relatório GLPI App

Um aplicativo desktop moderno, focado em performance e produtividade, construído para facilitar a extração e visualização de chamados do sistema **GLPI** através de sua API REST.

## 🚀 Tecnologias e Arquitetura

O projeto foi desenvolvido focando em boas práticas de engenharia de software e separação de responsabilidades:

*   **Linguagem:** C# (.NET)
*   **Interface Gráfica:** Avalonia UI (Cross-platform)
*   **Arquitetura:** MVVM (Model-View-ViewModel) utilizando o `CommunityToolkit.Mvvm`
*   **Integração:** Consumo de API REST via `HttpClient` assíncrono
*   **Manipulação de Dados:** `System.Text.Json`

## ✨ Funcionalidades Atuais

- [x] Interface de login reativa e validada via MVVM.
- [x] Autenticação segura na API do GLPI (Suporte a App-Token e User-Token).
- [x] Geração e captura automática de `session_token`.
- [x] Sistema de Logs centralizado para monitoramento da comunicação HTTP.
- [ ] Módulo de extração e listagem de chamados (Em desenvolvimento).

## 🛠️ Como Executar o Projeto

### Pré-requisitos
*   [.NET SDK](https://dotnet.microsoft.com/download) instalado (versão mais recente).
*   Um servidor GLPI com a **API REST habilitada**.
*   Credenciais de API do GLPI (`App-Token` e `User-Token`).

### Passos para rodar localmente

1. Clone este repositório:
   ```bash
   git clone [https://github.com/SEU-USUARIO/RelatorioGLPIApp.git](https://github.com/SEU-USUARIO/RelatorioGLPIApp.git)
