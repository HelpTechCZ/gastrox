; =====================================================================
; Gastrox - Inno Setup Installer Script
; Instalace do C:\Gastrox + ikona na plochu + Start menu
; =====================================================================

#define MyAppName "Gastrox"
#define MyAppPublisher "HelpTech.cz"
#define MyAppURL "https://github.com/HelpTechCZ/gastrox"
#define MyAppExeName "Gastrox.exe"
; Verze se předává jako parametr z CI: /DMyAppVersion=0.5.0
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

[Setup]
AppId={{B7A3F2E1-4D5C-6789-ABCD-GASTROX00001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={sd}\Gastrox
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=Gastrox-{#MyAppVersion}-setup
SetupIconFile=gastrox.ico
UninstallDisplayIcon={app}\Gastrox.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile=wizard-sidebar.bmp
WizardSmallImageFile=wizard-small.bmp
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Povolit uzivateli zmenit slozku, ale default je C:\Gastrox
DisableDirPage=no

[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Vsechen publish output
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Ikona (pro zástupce)
Source: "gastrox.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start menu
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\gastrox.ico"; Comment: "Evidence skladových zásob"
Name: "{group}\Odinstalovat {#MyAppName}"; Filename: "{uninstallexe}"
; Plocha (desktop)
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\gastrox.ico"; Comment: "Evidence skladových zásob"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Zachovat databazi sklad.db pri upgrade (neprepsat)
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Po instalaci — pokud existuje stary sklad.db, nechat ho
  // (Inno Setup s ignoreversion neprepise soubory ktere nejsou ve [Files])
end;

[InstallDelete]
; Vycistit stare soubory pred upgrade (ale NIKDY sklad.db!)
Type: filesandordirs; Name: "{app}\*.dll"
Type: filesandordirs; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\runtimes"
; Ponechame: sklad.db, crash.log, gastrox.ico
