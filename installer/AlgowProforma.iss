; Algow Proforma PDF - Inno Setup script (ASCII-only metadata for Windows registry compatibility)
; Builds a per-user installer for the self-contained win-x64 publish output.
;
; Build adimlari:
;   dotnet publish AyTeknikKatalog/AyTeknikKatalog.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
;   (vc_redist.x64.exe bu klasore indirilmeli; repoda tutulmaz)
;   ISCC installer/AlgowProforma.iss

#define AppName         "Algow Proforma PDF"
#define AppVersion      "1.0.0"
#define AppPublisher    "AlgowAI"
#define AppExeName      "AlgowProforma.exe"
#define SourceDir       "..\publish\win-x64"
#define AppIcon         "..\AyTeknikKatalog\Resources\AppIcon.ico"
#define AppURL          "https://algow.net"

[Setup]
AppId={{A1F0B7C2-8D34-4E61-9A52-3C7E1D9F6B40}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
AppCopyright=Copyright (C) AlgowAI 2026
DefaultDirName={localappdata}\Programs\AlgowProforma
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
DisableReadyPage=no
DisableFinishedPage=no
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
OutputDir=..\installer\out
OutputBaseFilename=AlgowProforma-Setup-{#AppVersion}
SetupIconFile={#AppIcon}

Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes

WizardStyle=modern
WizardSizePercent=100

LanguageDetectionMethod=none
ShowLanguageDialog=no

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

VersionInfoVersion=1.0.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Kurulum Sihirbazi
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright=Copyright (C) AlgowAI 2026
VersionInfoOriginalFileName=AlgowProforma-Setup.exe

CloseApplications=force
RestartApplications=yes
SetupMutex=AlgowProforma_Setup_Mutex_{#AppVersion}
AppMutex=AlgowProforma_Running_Mutex
UsedUserAreasWarning=no

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaustune kisayol olustur"; GroupDescription: "Ek secenekler:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsVCRedist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} Kaldir"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/quiet /norestart"; StatusMsg: "Microsoft Visual C++ kuruluyor..."; Check: NeedsVCRedist; Flags: waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Algow Proforma PDF'i baslat"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function NeedsVCRedist: Boolean;
var
  Installed: Cardinal;
begin
  Result := True;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
    if Installed = 1 then Result := False;
end;
