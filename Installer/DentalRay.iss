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
function PrepareDentalRayDatabase: Boolean;
var HelperExe, HelperDirectory, ScriptPath, CheckFile, StateText: String; StateAnsi: AnsiString; ResultCode: Integer; Params: String;
begin
    Result := False;
    HelperDirectory := ExpandConstant('{tmp}\DentalRay.SetupHelper');
    HelperExe := HelperDirectory + '\DentalRay.SetupHelper.exe';
    ScriptPath := HelperDirectory + '\Database\DentalRay.Database.Install.sql';
    CheckFile := ExpandConstant('{tmp}\DentalRay.DatabaseState.txt');
    CreateDatabaseConfirmed := False;

    Params := '--server "' + Trim(SqlPage.Values[0]) + '" --check-database --output "' + CheckFile + '"';
    if not RunHiddenAndWait(HelperExe, Params, HelperDirectory, ResultCode) then
    begin
        MsgBox('بررسی دیتابیس DentalRay با خطا مواجه شد.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;

    if ResultCode = 20 then
    begin
        MsgBox('SQL Server روی این رایانه نصب نیست یا قابل دسترسی نیست.' + CRLF + CRLF + 'نصب DentalRay خاتمه یافت.', mbError, MB_OK);
        Exit;
    end;

    if ResultCode <> 0 then
    begin
        MsgBox('بررسی SQL Server موفق نبود.' + CRLF + 'کد خطا: ' + IntToStr(ResultCode) + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;

    StateText := '';
    StateAnsi := '';
    if not LoadStringFromFile(CheckFile, StateAnsi) then
    begin
        MsgBox('نتیجه بررسی دیتابیس دریافت نشد.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;
    StateText := Trim(String(StateAnsi));

    if Pos('MISSING', UpperCase(StateText)) > 0 then
    begin
        if MsgBox('دیتابیس DentalRay در SQL Server انتخاب‌شده پیدا نشد.' + CRLF + CRLF + 'آیا می‌خواهید دیتابیس DentalRay ایجاد و آماده‌سازی شود؟', mbConfirmation, MB_YESNO) <> IDYES then
        begin
            MsgBox('نصب توسط کاربر لغو شد.', mbInformation, MB_OK);
            Exit;
        end;
        CreateDatabaseConfirmed := True;
    end;

    Params := '--server "' + Trim(SqlPage.Values[0]) + '" --script "' + ScriptPath + '"';
    if CreateDatabaseConfirmed then
        Params := Params + ' --create-database true';

    if not RunHiddenAndWait(HelperExe, Params, HelperDirectory, ResultCode) or (ResultCode <> 0) then
    begin
        if ResultCode = 20 then
            MsgBox('SQL Server قابل دسترسی نیست.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK)
        else if ResultCode = 30 then
            MsgBox('دیتابیس DentalRay وجود ندارد و ایجاد آن تأیید نشده است.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK)
        else
            MsgBox('آماده‌سازی دیتابیس DentalRay ناموفق بود.' + CRLF + 'کد خطا: ' + IntToStr(ResultCode) + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
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
