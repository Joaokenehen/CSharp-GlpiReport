# Relatório GLPI App

Um aplicativo desktop moderno, focado em performance e produtividade, construído para facilitar a extração, visualização e análise de dados de chamados do sistema **GLPI** através de sua API REST.

## Download

Você pode baixar a versão mais recente do aplicativo diretamente da página de **Releases**:

**[Baixar a Última Versão](https://github.com/SEU-USUARIO/RelatorioGLPIApp/releases/latest)**

## Tecnologias e Arquitetura

O projeto foi desenvolvido focando em boas práticas de engenharia de software e separação de responsabilidades:

*   **Linguagem:** C# (.NET)
*   **Interface Gráfica:** Avalonia UI (Cross-platform)
*   **Arquitetura:** MVVM (Model-View-ViewModel) utilizando o `CommunityToolkit.Mvvm`
*   **Integração:** Consumo de API REST via `HttpClient` assíncrono para GLPI.
*   **Manipulação de Dados:** `System.Text.Json` para serialização/desserialização.
*   **Geração de Documentos:** QuestPDF para PDF e DocX para Word.

## Funcionalidades Atuais

- [x] Interface de login reativa e validada via MVVM.
- [x] Autenticação segura na API do GLPI (Suporte a App-Token e User-Token).
- [x] Geração e captura automática de `session_token`.
- [x] Sistema de Logs centralizado para monitoramento da comunicação HTTP.
- [x] **Dashboard:** Visão geral dos chamados do dia, com filtros por data e usuário. Adição manual de itens. Exportação para PDF/Word e cópia para área de transferência. Salvamento e carregamento de estados do relatório.
- [x] **Relatórios Gerais:** Geração de relatórios detalhados por período, incluindo estatísticas de chamados (abertos, solucionados, plantão, tempo médio), produtividade por técnico (com ordenação clicável) e demandas por setor (Matriz, Agências, Filiais, Garagem, Encomendas, Agências Próprias). Salvamento, carregamento, exclusão, exportação (PDF/Word) e impressão.
- [x] **Relatórios de Técnicos:** Tela dedicada à produtividade dos técnicos, com relatórios do dia ou completos. Produtividade por técnico com ordenação clicável. Salvamento, carregamento, exclusão, exportação (PDF/Word) e impressão.
- [x] **Detalhes do Técnico:** Ao clicar em um técnico, exibe a lista de chamados solucionados por ele. Cada chamado pode ser expandido para visualizar o histórico completo da conversa (follow-ups e soluções) diretamente da API do GLPI.
- [x] **Navegação Consistente:** Botões de navegação padronizados no cabeçalho de todas as telas principais (Dashboard, Relatórios Gerais, Relatórios de Técnicos).
- [x] **Controles de Janela:** Botões de minimizar, maximizar e fechar presentes em todas as telas, incluindo a de login e detalhes.
- [x] **Persistência de Dados:** Credenciais de login e estados de relatórios podem ser salvos localmente.
- [x] **UI/UX:** Design limpo e responsivo com Avalonia UI, feedback visual para ações de carregamento e notificações.

## Como Executar o Projeto

### Pré-requisitos
*   [.NET SDK](https://dotnet.microsoft.com/download) instalado (versão mais recente).
*   Um servidor GLPI com a **API REST habilitada**.
*   Credenciais de API do GLPI (`App-Token` e `User-Token`).

### Passos para rodar localmente

1.  Clone este repositório:
    ```bash
    git clone https://github.com/Joaokenehen/RelatorioGLPIApp.git
    ```
2.  Navegue até o diretório do projeto:
    ```bash
    cd RelatorioGLPIApp
    ```
3.  Execute o aplicativo:
    ```bash
    dotnet run
    ```

### Publicar para Distribuição

Para gerar um executável independente (não requer instalação do .NET no cliente):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
O executável será gerado na pasta `bin/Release/netX.Y/win-x64/publish/`.
