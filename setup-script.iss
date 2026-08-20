#define MyAppName "Gerador de Relatórios GLPI"
#define MyAppVersion "1.5.3"
#define MyAppPublisher "João Gustavo"
#define MyAppExeName "RelatorioGLPIApp.exe"

[Setup]
AppId={{8A3D1B9C-4D2F-4B1E-9C8A-5E7B1F3A0D9C}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher="{#MyAppPublisher}"
DefaultDirName={autopf}\{#MyAppName}
SetupIconFile=.\Assets\logocsharp.ico
DisableProgramGroupPage=yes
CloseApplications=yes
OutputDir=C:\Users\joaot\Desktop\Projetos\RelatorioGLPIApp\Installer
OutputBaseFilename=setup-relatorio-glpi-v1.5.3
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\Users\joaot\Desktop\Projetos\RelatorioGLPIApp\bin\Release\net8.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function GetUninstallString(): String;
var
  ValueName: String;
  UninstallPath: String;
begin
  // Busca a chave de desinstalação usando o AppId do seu projeto
  ValueName := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8A3D1B9C-4D2F-4B1E-9C8A-5E7B1F3A0D9C}_is1';
  UninstallPath := '';
  
  // Procura no Registro do Windows (64-bit/32-bit e por Usuário)
  if not RegQueryStringValue(HKLM, ValueName, 'UninstallString', UninstallPath) then
    RegQueryStringValue(HKCU, ValueName, 'UninstallString', UninstallPath);
    
  Result := UninstallPath;
end;

function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

procedure InitializeWizard();
var
  UninstallString: String;
  ResultCode: Integer;
begin
  // Se detectar uma versão anterior instalada com o mesmo AppId
  if IsUpgrade() then
  begin
    UninstallString := RemoveQuotes(GetUninstallString());
    
    // Executa o desinstalador antigo em modo silencioso antes de continuar
    Exec(UninstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;