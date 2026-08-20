; DeepSeek Harness Launcher v1.0.0 安装脚本
; 用户级安装（安装到 LocalAppData，无需管理员，保持 config.json / logs 可写）

[Setup]
AppId={{8F2E6B7A-3C1D-4E9F-9A2B-000000000001}
AppName=DeepSeek Harness Launcher
AppVersion=1.0.0
AppVerName=DeepSeek Harness Launcher 1.0.0
AppPublisher=DeepSeek Harness
DefaultDirName={localappdata}\DeepSeek Harness Launcher
DefaultGroupName=DeepSeek Harness Launcher
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir=.
OutputBaseFilename=DeepSeekHarnessLauncher_Setup_v1.0.0
SetupIconFile=..\src\DeepSeekHarnessLauncher\Resources\app.ico
UninstallDisplayIcon={app}\DeepSeekHarnessLauncher.exe
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式(&D)"; GroupDescription: "附加任务："

[Files]
; 排除运行日志（含本机绝对路径/用户目录等隐私）与调试符号（*.pdb 内嵌本地源码路径）。
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "logs\*,*.pdb"
Source: "..\nodejs\*.msi"; DestDir: "{app}\nodejs"; Flags: ignoreversion

[Icons]
Name: "{group}\DeepSeek Harness Launcher"; Filename: "{app}\DeepSeekHarnessLauncher.exe"
Name: "{group}\卸载 DeepSeek Harness Launcher"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DeepSeek Harness Launcher"; Filename: "{app}\DeepSeekHarnessLauncher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DeepSeekHarnessLauncher.exe"; Description: "立即运行 DeepSeek Harness Launcher(&L)"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: files; Name: "{app}\config.json"
