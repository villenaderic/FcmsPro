; FCMS Pro - Windows Installer (Inno Setup)
;
; Prerequisites before running this:
;   1. dotnet publish ..\..\src\FcmsPro.Avalonia\FcmsPro.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
;   2. Have Inno Setup 6+ installed (https://jrsoftware.org/isinfo.php)
;
; Build with: iscc fcmspro.iss
; Output: .\Output\FcmsPro-Setup-{version}.exe

#define MyAppName "FCMS Pro"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Roderic Villena"
#define MyAppExeName "FcmsPro.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Icon file: generated from the app-icon-source.png design asset via
; installers/../src/FcmsPro.Avalonia/Assets/app.ico (multi-resolution ICO).
SetupIconFile=..\..\src\FcmsPro.Avalonia\Assets\app.ico
OutputDir=Output
OutputBaseFilename=FcmsPro-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Assumes the self-contained single-file publish output described above
; lands in .\publish relative to this .iss file.
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
