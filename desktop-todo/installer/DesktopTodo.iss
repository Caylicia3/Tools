#ifndef Payload
#define Payload "..\artifacts\DesktopTodo"
#endif
#ifndef OutputName
#define OutputName "DesktopTodo-0.1.3.2-win-x64-Setup"
#endif

[Setup]
AppId={{36E96763-9573-4B93-B494-058BF5DDF380}
AppName=日程管理
AppVersion=0.1.3.2
AppPublisher=DesktopTodo
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=日程管理
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\artifacts
OutputBaseFilename={#OutputName}
SetupIconFile=..\src\DesktopTodo.App\Assets\app.ico
UninstallDisplayIcon={app}\DesktopTodo.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimp"; MessagesFile: "..\.tools\InnoSetup\Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
Name: "startmenu"; Description: "创建开始菜单快捷方式"; Flags: unchecked

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,data-location.txt"

[Icons]
Name: "{autodesktop}\日程管理"; Filename: "{app}\DesktopTodo.exe"; Tasks: desktopicon
Name: "{autoprograms}\日程管理"; Filename: "{app}\DesktopTodo.exe"; Tasks: startmenu

[Run]
Filename: "{app}\DesktopTodo.exe"; Description: "打开日程管理"; Flags: nowait postinstall skipifsilent unchecked

[Code]
var
 DataPage: TInputDirWizardPage;
 LastSuggestion, RequestedData: String;

function DefaultInstallDir(Param: String): String;
begin
 if DirExists('E:\') then Result := 'E:\Apps\DesktopTodo'
 else if DirExists('D:\') then Result := 'D:\Apps\DesktopTodo'
 else Result := ExpandConstant('{userpf}\DesktopTodo');
end;

function ExistingData: String;
var Raw: AnsiString;
begin
 Result := '';
 if LoadStringFromFile(AddBackslash(WizardDirValue) + 'data-location.txt', Raw) then begin
  Result := Trim(Utf8Decode(Raw));
  if (Length(Result) > 0) and (Result[1] = #$FEFF) then Delete(Result, 1, 1);
 end;
end;

procedure UpdateDataSuggestion;
var Existing, Suggested: String;
begin
 Existing := ExistingData;
 if Existing <> '' then begin
  DataPage.Values[0] := Existing;
  DataPage.Edits[0].Enabled := False;
  DataPage.Buttons[0].Enabled := False;
  DataPage.Description := '升级将沿用已有的数据位置；卸载会保留数据。';
 end else begin
  DataPage.Edits[0].Enabled := True;
  DataPage.Buttons[0].Enabled := True;
  DataPage.Description := '日程、任务、待办笔记和备份保存在此处。请选择程序目录之外的位置。';
  Suggested := AddBackslash(ExtractFileDir(RemoveBackslashUnlessRoot(WizardDirValue))) + 'DesktopTodoData';
  if RequestedData <> '' then Suggested := RequestedData;
  if (DataPage.Values[0] = '') or (DataPage.Values[0] = LastSuggestion) then DataPage.Values[0] := Suggested;
  LastSuggestion := Suggested;
 end;
end;

procedure InitializeWizard;
begin
 RequestedData := ExpandConstant('{param:DATADIR|}');
 DataPage := CreateInputDirPage(wpSelectDir, '选择数据保存位置',
  '日程、任务、待办笔记和备份会保存在这里。',
  '新安装默认建议保存在程序目录旁边，可更改。升级沿用已有数据位置。程序仅手动启动；卸载保留数据。',
  False, '');
 DataPage.Add('数据文件夹：');
 UpdateDataSuggestion;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
 if CurPageID = DataPage.ID then UpdateDataSuggestion;
end;

function ValidatePaths: String;
var AppPath, DataPath: String;
begin
 Result := '';
 if (Trim(DataPage.Values[0]) = '') or (ExtractFileDrive(DataPage.Values[0]) = '') then begin
  Result := '请选择完整的数据文件夹路径。'; Exit;
 end;
 AppPath := AddBackslash(ExpandFileName(WizardDirValue));
 DataPath := AddBackslash(ExpandFileName(DataPage.Values[0]));
 if Pos(Lowercase(AppPath), Lowercase(DataPath)) = 1 then
  Result := '请把数据保存在程序安装目录之外，以便安全更新与卸载。';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var ErrorText: String;
begin
 Result := True;
 if CurPageID = wpSelectDir then UpdateDataSuggestion;
 if CurPageID = DataPage.ID then begin
  ErrorText := ValidatePaths;
  if ErrorText <> '' then begin
   SuppressibleMsgBox(ErrorText, mbError, MB_OK, IDOK);
   Result := False;
  end;
 end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
 UpdateDataSuggestion;
 Result := ValidatePaths;
 if (Result = '') and not ForceDirectories(DataPage.Values[0]) then
  Result := '无法创建数据文件夹，请选择可写入的位置。';
end;

// File-system rename alone can leave Explorer showing an old, unopenable desktop item.
// Pass Unicode paths and flush the notification before setup completes.
procedure NotifyShortcutChange(EventID: Integer; Flags: Cardinal; Item1, Item2: String);
 external 'SHChangeNotify@shell32.dll stdcall';

procedure RenameOwnedShortcut(OldPath, NewPath: String);
var Shell, Link: Variant;
begin
 if not FileExists(OldPath) then Exit;
 try
  Shell := CreateOleObject('WScript.Shell');
  Link := Shell.CreateShortcut(OldPath);
  if CompareText(ExpandFileName(Link.TargetPath), ExpandConstant('{app}\DesktopTodo.exe')) = 0 then begin
   if not FileExists(NewPath) then begin
    Link.Description := '日程管理';
    Link.Save;
    if RenameFile(OldPath, NewPath) then
     NotifyShortcutChange(1, $1005, OldPath, NewPath)
    else Log('Could not rename old shortcut: ' + OldPath);
   end else if DeleteFile(OldPath) then
    NotifyShortcutChange(4, $1005, OldPath, '');
  end;
 except
  Log('Could not inspect old shortcut: ' + OldPath);
 end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
 if CurStep = ssPostInstall then begin
  if not SaveStringToFile(ExpandConstant('{app}\data-location.txt'), UTF8Encode(DataPage.Values[0]), False) then
   RaiseException('无法保存数据目录设置，请检查程序目录写入权限。');
  RenameOwnedShortcut(ExpandConstant('{autodesktop}\日有安排.lnk'), ExpandConstant('{autodesktop}\日程管理.lnk'));
  RenameOwnedShortcut(ExpandConstant('{autoprograms}\日有安排.lnk'), ExpandConstant('{autoprograms}\日程管理.lnk'));
 end;
end;

