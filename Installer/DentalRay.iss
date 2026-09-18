#define MyAppName "DentalRay"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Rahim Namazi"
#define MyAppExeName "DentalRay.Api.exe"
#ifndef SourceRoot
#define SourceRoot ".."
#endif


[Setup]

AppId={{03464487-C1B2-46F0-89E2-3AFAEE74FD1F}

AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\DentalRay

DisableDirPage=yes
DisableProgramGroupPage=yes

PrivilegesRequired=admin

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=Output
OutputBaseFilename=DentalRay_Setup_1.0.0

Compression=lzma2
SolidCompression=yes

WizardStyle=modern hidebevels
WizardBackColor=#F7FBFF
WizardImageBackColor=#DDF4F5
WizardSmallImageBackColor=#DDF4F5
WizardSizePercent=110

UninstallDisplayIcon={app}\{#MyAppExeName}


[Languages]

Name: "persian"; MessagesFile: "Farsi.isl"


[LangOptions]

persian.LanguageName=فارسی
persian.DialogFontName=Segoe UI
persian.DialogFontSize=9
persian.WelcomeFontName=Segoe UI
persian.WelcomeFontSize=16
persian.RightToLeft=yes


[Files]

Source: "{#SourceRoot}\DentalRay\*"; DestDir: "{app}"; Excludes: "appsettings.json,appsettings.Development.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\DentalRay.SetupHelper\*"; DestDir: "{tmp}\DentalRay.SetupHelper"; Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall


[Icons]

Name: "{autoprograms}\DentalRay"; Filename: "{sys}\rundll32.exe"; Parameters: "url.dll,FileProtocolHandler http://localhost:5202"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\DentalRay"; Filename: "{sys}\rundll32.exe"; Parameters: "url.dll,FileProtocolHandler http://localhost:5202"; IconFilename: "{app}\{#MyAppExeName}"


[UninstallRun]

Filename: "{sys}\sc.exe"; Parameters: "stop DentalRay"; Flags: runhidden waituntilterminated; RunOnceId: "StopDentalRayService"
Filename: "{sys}\sc.exe"; Parameters: "delete DentalRay"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteDentalRayService"


[Code]

const
    CRLF = #13#10;

var
    SqlPage: TInputQueryWizardPage;
    StoragePage: TInputDirWizardPage;
    CreateDatabaseConfirmed: Boolean;
    DiscoveredDatabaseState: String;

function RunHiddenAndWait(FileName: String; Parameters: String; WorkingDirectory: String; var ResultCode: Integer): Boolean; forward;
function DiscoverDentalRaySqlServer: Boolean; forward;
function PrepareDentalRayDatabase: Boolean; forward;

procedure InitializeWizard;
begin
    WizardForm.Font.Name := 'Segoe UI';
    WizardForm.Font.Size := 9;
    WizardForm.Color := $00FBFDFF;
    WizardForm.WelcomeLabel1.Font.Name := 'Segoe UI';
    WizardForm.WelcomeLabel1.Font.Size := 16;
    WizardForm.WelcomeLabel1.Font.Style := [fsBold];

    SqlPage := CreateInputQueryPage(wpSelectDir, 'SQL Server', 'SQL Server مورد استفاده DentalRay را مشخص کنید.', 'نام Server و Instance را وارد کنید.' + CRLF + 'مثال: .\DENTALRAY');
    SqlPage.Add('نام Server / Instance:', False);
    SqlPage.Values[0] := '.\DENTALRAY';

    StoragePage := CreateInputDirPage(SqlPage.ID, 'محل ذخیره تصاویر', 'محل ذخیره تصاویر رادیولوژی را مشخص کنید.', 'تمام تصاویر بیماران در این پوشه نگهداری خواهند شد.' + CRLF + 'در صورت نیاز می‌توانید این مسیر را تغییر دهید.', False, '');
    StoragePage.Add('مسیر ذخیره تصاویر رادیولوژی:');
    StoragePage.Values[0] := 'D:\RadiologyData';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
    Result := True;

    if CurPageID = SqlPage.ID then
    begin
        if Trim(SqlPage.Values[0]) = '' then
        begin
            MsgBox('لطفاً نام SQL Server را وارد کنید.', mbError, MB_OK);
            Result := False;
            Exit;
        end;

        if Trim(SqlPage.Values[0]) = '.\\DENTALRAY' then
        begin
            if not DiscoverDentalRaySqlServer then
            begin
                Result := False;
                Exit;
            end;
        end;

        if not PrepareDentalRayDatabase then
        begin
            Result := False;
            Exit;
        end;
    end;

    if CurPageID = StoragePage.ID then
    begin
        if Trim(StoragePage.Values[0]) = '' then
        begin
            MsgBox('لطفاً مسیر ذخیره تصاویر را مشخص کنید.', mbError, MB_OK);
            Result := False;
            Exit;
        end;
    end;
end;

function JsonEscape(S: String): String;
var I: Integer; C: Char;
begin
    Result := '';
    for I := 1 to Length(S) do
    begin
        C := S[I];
        if C = '\\' then Result := Result + '\\\\'
        else if C = '"' then Result := Result + '\\"'
        else Result := Result + C;
    end;
end;

function RunHiddenAndWait(FileName: String; Parameters: String; WorkingDirectory: String; var ResultCode: Integer): Boolean;
begin
    Result := Exec(FileName, Parameters, WorkingDirectory, SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ResultCode: Integer;
begin
    Result := '';
    RunHiddenAndWait(ExpandConstant('{sys}\sc.exe'), 'stop DentalRay', '', ResultCode);
    Sleep(1000);
    RunHiddenAndWait(ExpandConstant('{sys}\sc.exe'), 'delete DentalRay', '', ResultCode);
    Sleep(1000);
end;

function DiscoverDentalRaySqlServer: Boolean;
var HelperExe, HelperDirectory, OutputFile, StateText: String; ResultCode: Integer;
begin
    Result := False;
    HelperDirectory := ExpandConstant('{tmp}\DentalRay.SetupHelper');
    HelperExe := HelperDirectory + '\DentalRay.SetupHelper.exe';
    OutputFile := ExpandConstant('{tmp}\DentalRay.SqlDiscovery.txt');

    if not FileExists(HelperExe) then
    begin
        MsgBox('ابزار بررسی SQL Server پیدا نشد.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;

    if not RunHiddenAndWait(HelperExe, '--discover --output "' + OutputFile + '"', HelperDirectory, ResultCode) or (ResultCode <> 0) then
    begin
        MsgBox('هیچ SQL Server قابل دسترسی پیدا نشد.' + CRLF + 'لطفاً SQL Server را بررسی کنید و دوباره تلاش کنید.' + CRLF + CRLF + 'نصب تا رفع مشکل ادامه نخواهد یافت.', mbError, MB_OK);
        Exit;
    end;

    if not LoadStringFromFile(OutputFile, StateText) then
    begin
        MsgBox('نتیجه بررسی SQL Server دریافت نشد.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;

    if Pos(#10, StateText) > 0 then
        SqlPage.Values[0] := Trim(Copy(StateText, 1, Pos(#10, StateText) - 1))
    else
        SqlPage.Values[0] := Trim(StateText);

    DiscoveredDatabaseState := StateText;
    Result := True;
end;

function PrepareDentalRayDatabase: Boolean;
var HelperExe, HelperDirectory: String; ResultCode: Integer; Exists: Boolean; Params: String;
begin
    Result := False;
    HelperDirectory := ExpandConstant('{tmp}\DentalRay.SetupHelper');
    HelperExe := HelperDirectory + '\DentalRay.SetupHelper.exe';

    Exists := Pos('EXISTS', UpperCase(DiscoveredDatabaseState)) > 0;

    if not Exists then
    begin
        if MsgBox('دیتابیس DentalRay در SQL Server انتخاب‌شده پیدا نشد.' + CRLF + CRLF + 'آیا می‌خواهید دیتابیس DentalRay ایجاد و آماده‌سازی شود؟', mbConfirmation, MB_YESNO) <> IDYES then
        begin
            MsgBox('نصب توسط کاربر لغو شد.', mbInformation, MB_OK);
            Exit;
        end;
        CreateDatabaseConfirmed := True;
    end;

    Params := '--server "' + Trim(SqlPage.Values[0]) + '"';
    if CreateDatabaseConfirmed then Params := Params + ' --create-database true';

    if not RunHiddenAndWait(HelperExe, Params, HelperDirectory, ResultCode) or (ResultCode <> 0) then
    begin
        if ResultCode = 20 then
            MsgBox('SQL Server روی این رایانه نصب نیست یا قابل دسترسی نیست.' + CRLF + CRLF + 'لطفاً ابتدا SQL Server را نصب و سرویس آن را فعال کنید.' + CRLF + CRLF + 'نصب DentalRay خاتمه یافت.', mbError, MB_OK)
        else if ResultCode = 21 then
            MsgBox('هیچ SQL Server قابل دسترسی پیدا نشد.' + CRLF + CRLF + 'لطفاً SQL Server را نصب و سرویس آن را فعال کنید.' + CRLF + CRLF + 'نصب DentalRay خاتمه یافت.', mbError, MB_OK)
        else
            MsgBox('آماده‌سازی دیتابیس DentalRay ناموفق بود.' + CRLF + CRLF + 'کد خطا: ' + IntToStr(ResultCode) + CRLF + CRLF + 'نصب تا رفع مشکل ادامه نخواهد یافت.', mbError, MB_OK);
        Exit;
    end;

    Result := True;
end;

procedure ConfigureDentalRay;
var SqlServer, StoragePath, HelperExe, HelperDirectory, ConfigDirectory, ConfigFile, ConfigText, DentalRayExe: String; ResultCode: Integer;
begin
    SqlServer := Trim(SqlPage.Values[0]);
    StoragePath := Trim(StoragePage.Values[0]);
    HelperDirectory := ExpandConstant('{tmp}\DentalRay.SetupHelper');
    HelperExe := HelperDirectory + '\DentalRay.SetupHelper.exe';

    if not FileExists(HelperExe) then RaiseException('DentalRay SetupHelper پیدا نشد.');

    if not RunHiddenAndWait(HelperExe, '--server "' + SqlServer + '"', HelperDirectory, ResultCode) then RaiseException('SetupHelper اجرا نشد.');
    if ResultCode <> 0 then RaiseException('آماده‌سازی دیتابیس انجام نشد.' + CRLF + 'SetupHelper Exit Code: ' + IntToStr(ResultCode));

    if not ForceDirectories(StoragePath) then RaiseException('امکان ایجاد پوشه تصاویر وجود ندارد:' + CRLF + StoragePath);

    if not RunHiddenAndWait(ExpandConstant('{sys}\icacls.exe'), '"' + StoragePath + '" /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F"', '', ResultCode) then RaiseException('امکان تنظیم دسترسی پوشه تصاویر وجود ندارد.');
    if ResultCode <> 0 then RaiseException('تنظیم دسترسی پوشه تصاویر ناموفق بود.' + CRLF + 'Exit Code: ' + IntToStr(ResultCode));

    ConfigDirectory := ExpandConstant('{commonappdata}\DentalRay');
    if not ForceDirectories(ConfigDirectory) then RaiseException('امکان ایجاد پوشه تنظیمات DentalRay وجود ندارد.');
    ConfigFile := ConfigDirectory + '\DentalRay.config.json';

    ConfigText := '{' + CRLF +
        '  "ConnectionStrings": {' + CRLF +
        '    "DentalRay": "Server=' + JsonEscape(SqlServer) + ';Database=DentalRay;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"' + CRLF +
        '  },' + CRLF +
        '  "RadiologyStorage": {' + CRLF +
        '    "RootPath": "' + JsonEscape(StoragePath) + '"' + CRLF +
        '  },' + CRLF +
        '  "Urls": "http://0.0.0.0:5202",' + CRLF +
        '  "RemoteAccess": {' + CRLF +
        '    "LocalScheme": "http",' + CRLF +
        '    "LocalPort": 5202,' + CRLF +
        '    "PublicHost": "",' + CRLF +
        '    "PublicScheme": "http",' + CRLF +
        '    "PublicPort": 5202' + CRLF +
        '  },' + CRLF +
        '  "Logging": {' + CRLF +
        '    "LogLevel": {' + CRLF +
        '      "Default": "Information",' + CRLF +
        '      "Microsoft.AspNetCore": "Warning"' + CRLF +
        '    }' + CRLF +
        '  },' + CRLF +
        '  "AllowedHosts": "*"' + CRLF +
        '}' + CRLF;

    if not SaveStringToFile(ConfigFile, ConfigText, False) then RaiseException('فایل تنظیمات DentalRay ساخته نشد.');

    if not RunHiddenAndWait(ExpandConstant('{sys}\icacls.exe'), '"' + ConfigFile + '" /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F"', '', ResultCode) then RaiseException('امکان تنظیم دسترسی فایل DentalRay.config.json وجود ندارد.');
    if ResultCode <> 0 then RaiseException('تنظیم دسترسی فایل DentalRay.config.json ناموفق بود.' + CRLF + 'Exit Code: ' + IntToStr(ResultCode));

    DentalRayExe := ExpandConstant('{app}\DentalRay.Api.exe');
    if not RunHiddenAndWait(ExpandConstant('{sys}\sc.exe'), 'create DentalRay binPath= "' + DentalRayExe + '" start= auto DisplayName= "DentalRay"', '', ResultCode) then RaiseException('امکان اجرای دستور ایجاد Windows Service وجود ندارد.');
    if ResultCode <> 0 then RaiseException('Windows Service DentalRay ایجاد نشد.' + CRLF + 'Exit Code: ' + IntToStr(ResultCode));

    if not RunHiddenAndWait(ExpandConstant('{sys}\sc.exe'), 'description DentalRay "DentalRay Dental Radiology Service"', '', ResultCode) then RaiseException('امکان تنظیم توضیحات Windows Service وجود ندارد.');
    if ResultCode <> 0 then RaiseException('توضیحات Windows Service تنظیم نشد.' + CRLF + 'Exit Code: ' + IntToStr(ResultCode));

    if not RunHiddenAndWait(ExpandConstant('{sys}\sc.exe'), 'start DentalRay', '', ResultCode) then RaiseException('امکان اجرای Windows Service وجود ندارد.');
    if ResultCode <> 0 then RaiseException('Windows Service DentalRay ایجاد شد ولی Start نشد.' + CRLF + 'Exit Code: ' + IntToStr(ResultCode));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
    if CurStep = ssPostInstall then ConfigureDentalRay;
end;
