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
; Ikona setup.exe souboru (generována v CI z grafika/ikona.png)
SetupIconFile=gastrox.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile=wizard-sidebar.bmp
WizardSmallImageFile=wizard-small.bmp
PrivilegesRequired=admin
CloseApplications=force
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableDirPage=no

[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"

[Files]
; Publish output (celá aplikace)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Ikona jako samostatný soubor (pro zástupce na ploše/Start menu)
Source: "gastrox.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Plocha aktuálního uživatele — VŽDY (i při tiché instalaci/aktualizaci)
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName},0"; Comment: "Evidence skladových zásob"
; Start menu
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName},0"; Comment: "Evidence skladových zásob"
Name: "{group}\Odinstalovat {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[InstallDelete]
; Vyčistit staré soubory před upgrade (ale NIKDY sklad.db!)
Type: filesandordirs; Name: "{app}\*.dll"
Type: filesandordirs; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\runtimes"
