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

; ============================================================
; DentalRay Main Application
; ============================================================
;
; appsettings.json و appsettings.Development.json
; عمداً داخل Setup قرار داده نمی‌شوند.
;
; تنظیمات واقعی هنگام نصب در این مسیر ساخته می‌شوند:
;
; C:\ProgramData\DentalRay\DentalRay.config.json
;
; ============================================================

Source: "{#SourceRoot}\DentalRay\*"; \
    DestDir: "{app}"; \
    Excludes: "appsettings.json,appsettings.Development.json"; \
    Flags: ignoreversion recursesubdirs createallsubdirs



; ============================================================
; DentalRay SetupHelper
; ============================================================
;
; SetupHelper فقط هنگام نصب موردنیاز است.
;
; فایل‌های آن داخل پوشه موقت Setup استخراج می‌شوند
; و بعد از پایان نصب باقی نمی‌مانند.
;
; ============================================================

Source: "{#SourceRoot}\DentalRay.SetupHelper\*"; \
    DestDir: "{tmp}\DentalRay.SetupHelper"; \
    Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall



[Icons]

; ============================================================
; Start Menu Shortcut
; ============================================================
;
; DentalRay.Api.exe مستقیماً از Shortcut اجرا نمی‌شود.
;
; برنامه به صورت Windows Service اجرا می‌شود و Shortcut
; فقط مرورگر را با آدرس DentalRay باز می‌کند.
;
; ============================================================

Name: "{autoprograms}\DentalRay"; \
    Filename: "{sys}\rundll32.exe"; \
    Parameters: "url.dll,FileProtocolHandler http://localhost:5202"; \
    IconFilename: "{app}\{#MyAppExeName}"



; ============================================================
; Desktop Shortcut
; ============================================================

Name: "{autodesktop}\DentalRay"; \
    Filename: "{sys}\rundll32.exe"; \
    Parameters: "url.dll,FileProtocolHandler http://localhost:5202"; \
    IconFilename: "{app}\{#MyAppExeName}"



[UninstallRun]

; ============================================================
; Remove Windows Service
; ============================================================
;
; هنگام Uninstall:
;
; 1. Windows Service متوقف می‌شود.
; 2. Service Registration حذف می‌شود.
;
; نکته مهم:
;
; Database حذف نمی‌شود.
; تصاویر رادیولوژی حذف نمی‌شوند.
; ProgramData نیز فعلاً حذف نمی‌شود.
;
; ============================================================

Filename: "{sys}\sc.exe"; \
    Parameters: "stop DentalRay"; \
    Flags: runhidden waituntilterminated; \
    RunOnceId: "StopDentalRayService"

Filename: "{sys}\sc.exe"; \
    Parameters: "delete DentalRay"; \
    Flags: runhidden waituntilterminated; \
    RunOnceId: "DeleteDentalRayService"



[Code]

const
    CRLF = #13#10;


var

    SqlPage:
        TInputQueryWizardPage;

    StoragePage:
        TInputDirWizardPage;

    CreateDatabaseConfirmed:
        Boolean;

    DiscoveredDatabaseState:
        String;



// ============================================================
// InitializeWizard
// ============================================================
//
// صفحات اختصاصی Installer:
//
// 1. SQL Server / Instance
// 2. محل ذخیره تصاویر رادیولوژی
//
// ============================================================

procedure InitializeWizard;

begin

    // ظاهر کلی فرم‌های نصب: فونت فارسی، راست‌چین و پس‌زمینه روشن و شاد.
    WizardForm.Font.Name := 'Segoe UI';
    WizardForm.Font.Size := 9;
    WizardForm.BiDiMode := bdRightToLeft;

    WizardForm.Color := $00FBFDFF;
    WizardForm.WelcomeLabel1.Font.Name := 'Segoe UI';
    WizardForm.WelcomeLabel1.Font.Size := 16;
    WizardForm.WelcomeLabel1.Font.Style := [fsBold];


    // --------------------------------------------------------
    // SQL Server
    // --------------------------------------------------------

    SqlPage :=
        CreateInputQueryPage(
            wpSelectDir,
            'SQL Server',
            'SQL Server مورد استفاده DentalRay را مشخص کنید.',
            'نام Server و Instance را وارد کنید.' +
            CRLF +
            'مثال: .\DENTALRAY'
        );


    SqlPage.Add(
        'نام Server / Instance:',
        False
    );


    SqlPage.Values[0] :=
        '.\DENTALRAY';



    // --------------------------------------------------------
    // Radiology Storage
    // --------------------------------------------------------

    StoragePage :=
        CreateInputDirPage(
            SqlPage.ID,
            'محل ذخیره تصاویر',
            'محل ذخیره تصاویر رادیولوژی را مشخص کنید.',
            'تمام تصاویر بیماران در این پوشه نگهداری خواهند شد.' + CRLF + 'در صورت نیاز می‌توانید این مسیر را تغییر دهید.',
            False,
            ''
        );


    StoragePage.Add(
        'مسیر ذخیره تصاویر رادیولوژی:'
    );


    StoragePage.Values[0] :=
        'D:\RadiologyData';

end;



// ============================================================
// NextButtonClick
// ============================================================
//
// قبل از رفتن به مرحله بعد بررسی می‌کنیم اطلاعات ضروری
// خالی نباشند.
//
// ============================================================

function NextButtonClick(
    CurPageID: Integer
): Boolean;

begin

    Result :=
        True;



    // --------------------------------------------------------
    // SQL Server validation
    // --------------------------------------------------------

    if CurPageID =
        SqlPage.ID then
    begin

        if Trim(SqlPage.Values[0]) = '' then
        begin
            MsgBox('لطفاً نام SQL Server را وارد کنید.', mbError, MB_OK);
            Result := False;
            Exit;
        end;

        // در صورت استفاده از مقدار پیش‌فرض، ابتدا Instance مناسب را پیدا می‌کنیم.
        if Trim(SqlPage.Values[0]) = '.\\DENTALRAY' then
        begin
            if not DiscoverDentalRaySqlServer then
            begin
                Result := False;
                Exit;
            end;
        end;

        // بررسی نهایی دیتابیس؛ در صورت نبودن آن، ایجاد فقط با تأیید صریح کاربر.
        if not PrepareDentalRayDatabase then
        begin
            Result := False;
            Exit;
        end;
    end;



    // --------------------------------------------------------
    // Storage path validation
    // --------------------------------------------------------

    if CurPageID =
        StoragePage.ID then
    begin

        if Trim(
            StoragePage.Values[0]
        ) = '' then
        begin

            MsgBox(
                'لطفاً مسیر ذخیره تصاویر را مشخص کنید.',
                mbError,
                MB_OK
            );


            Result :=
                False;


            Exit;

        end;

    end;

end;



// ============================================================
// JsonEscape
// ============================================================
//
// Backslash و Double Quote را برای JSON Escape می‌کند.
//
// مثال:
//
// D:\RadiologyData
//
// تبدیل می‌شود به:
//
// D:\\RadiologyData
//
// ============================================================

function JsonEscape(
    S: String
): String;

var

    I:
        Integer;

    C:
        Char;

begin

    Result :=
        '';


    for I :=
        1 to Length(S) do
    begin

        C :=
            S[I];


        if C = '\' then
        begin

            Result :=
                Result +
                '\\';

        end
        else
        if C = '"' then
        begin

            Result :=
                Result +
                '\"';

        end
        else
        begin

            Result :=
                Result +
                C;

        end;

    end;

end;



// ============================================================
// RunHiddenAndWait
// ============================================================
//
// یک برنامه را Hidden اجرا می‌کند و تا پایان آن منتظر می‌ماند.
//
// Exit Code در ResultCode قرار می‌گیرد.
//
// ============================================================

function RunHiddenAndWait(
    FileName: String;
    Parameters: String;
    WorkingDirectory: String;
    var ResultCode: Integer
): Boolean;

begin

    Result :=
        Exec(
            FileName,
            Parameters,
            WorkingDirectory,
            SW_HIDE,
            ewWaitUntilTerminated,
            ResultCode
        );

end;



// ============================================================
// PrepareToInstall
// ============================================================
//
// اگر نسخه قبلی DentalRay نصب شده باشد:
//
// Service متوقف و Service Registration قبلی حذف می‌شود.
//
// این کار باعث می‌شود فایل‌های برنامه هنگام Upgrade
// توسط Service قفل نباشند.
//
// نبودن Service خطا محسوب نمی‌شود.
//
// ============================================================

function PrepareToInstall(
    var NeedsRestart: Boolean
): String;

var

    ResultCode:
        Integer;

begin

    Result :=
        '';


    // --------------------------------------------------------
    // Stop old Service
    // --------------------------------------------------------

    RunHiddenAndWait(
        ExpandConstant(
            '{sys}\sc.exe'
        ),
        'stop DentalRay',
        '',
        ResultCode
    );


    Sleep(
        1000
    );



    // --------------------------------------------------------
    // Delete old Service registration
    // --------------------------------------------------------

    RunHiddenAndWait(
        ExpandConstant(
            '{sys}\sc.exe'
        ),
        'delete DentalRay',
        '',
        ResultCode
    );


    Sleep(
        1000
    );

end;



// ============================================================
// DiscoverDentalRaySqlServer
// ============================================================

function DiscoverDentalRaySqlServer: Boolean;
var
    HelperExe, HelperDirectory, OutputFile, StateText: String;
    ResultCode: Integer;
begin
    Result := False;
    HelperDirectory := ExpandConstant('{tmp}\\DentalRay.SetupHelper');
    HelperExe := HelperDirectory + '\\DentalRay.SetupHelper.exe';
    OutputFile := ExpandConstant('{tmp}\\DentalRay.SqlDiscovery.txt');

    if not FileExists(HelperExe) then
    begin
        MsgBox('ابزار بررسی SQL Server پیدا نشد.' + CRLF + 'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;

    if not RunHiddenAndWait(HelperExe,
        '--discover --output "' + OutputFile + '"',
        HelperDirectory, ResultCode) or (ResultCode <> 0) then
    begin
        MsgBox('هیچ SQL Server قابل دسترسی پیدا نشد.' + CRLF +
               'لطفاً SQL Server را بررسی کنید و دوباره تلاش کنید.' + CRLF + CRLF +
               'نصب تا رفع مشکل ادامه نخواهد یافت.', mbError, MB_OK);
        Exit;
    end;

    if not LoadStringFromFile(OutputFile, StateText) then
    begin
        MsgBox('نتیجه بررسی SQL Server دریافت نشد.' + CRLF +
               'نصب متوقف شد.', mbError, MB_OK);
        Exit;
    end;

    // خط اول نام Instance و خط دوم وضعیت دیتابیس است.
    if Pos(#10, StateText) > 0 then
        SqlPage.Values[0] := Trim(Copy(StateText, 1, Pos(#10, StateText) - 1))
    else
        SqlPage.Values[0] := Trim(StateText);

    DiscoveredDatabaseState := StateText;
    Result := True;
end;


// ============================================================
// PrepareDentalRayDatabase
// ============================================================

function PrepareDentalRayDatabase: Boolean;
var
    HelperExe, HelperDirectory: String;
    ResultCode: Integer;
    Exists: Boolean;
    Params: String;
begin
    Result := False;
    HelperDirectory := ExpandConstant('{tmp}\\DentalRay.SetupHelper');
    HelperExe := HelperDirectory + '\\DentalRay.SetupHelper.exe';

    Exists := Pos('EXISTS', UpperCase(DiscoveredDatabaseState)) > 0;

    if not Exists then
    begin
        if MsgBox(
            'دیتابیس DentalRay در SQL Server انتخاب‌شده پیدا نشد.' + CRLF + CRLF +
            'آیا می‌خواهید دیتابیس DentalRay ایجاد و آماده‌سازی شود؟',
            mbConfirmation, MB_YESNO) <> IDYES then
        begin
            MsgBox('نصب توسط کاربر لغو شد.', mbInformation, MB_OK);
            Exit;
        end;

        CreateDatabaseConfirmed := True;
    end;

    Params := '--server "' + Trim(SqlPage.Values[0]) + '"';
    if CreateDatabaseConfirmed then
        Params := Params + ' --create-database true';

    if not RunHiddenAndWait(HelperExe, Params, HelperDirectory, ResultCode) or
       (ResultCode <> 0) then
    begin
        MsgBox('آماده‌سازی دیتابیس DentalRay ناموفق بود.' + CRLF + CRLF +
               'کد خطا: ' + IntToStr(ResultCode) + CRLF + CRLF +
               'نصب تا رفع مشکل ادامه نخواهد یافت.', mbError, MB_OK);
        Exit;
    end;

    Result := True;
end;


// ============================================================
// ConfigureDentalRay
// ============================================================
//
// مراحل اصلی آماده‌سازی:
//
// 1. اجرای SetupHelper
// 2. ایجاد / آماده‌سازی Database
// 3. ایجاد پوشه تصاویر
// 4. تنظیم Permission پوشه تصاویر
// 5. ساخت DentalRay.config.json
// 6. تنظیم Permission فایل Configuration
// 7. ثبت Windows Service
// 8. تنظیم Description سرویس
// 9. Start کردن Windows Service
//
// ============================================================

procedure ConfigureDentalRay;

var

    SqlServer:
        String;

    StoragePath:
        String;


    HelperExe:
        String;

    HelperDirectory:
        String;


    ConfigDirectory:
        String;

    ConfigFile:
        String;

    ConfigText:
        String;


    DentalRayExe:
        String;


    ResultCode:
        Integer;

begin

    // ========================================================
    // User Settings
    // ========================================================

    SqlServer :=
        Trim(
            SqlPage.Values[0]
        );


    StoragePath :=
        Trim(
            StoragePage.Values[0]
        );



    // ========================================================
    // 1. Database Setup
    // ========================================================

    HelperDirectory :=
        ExpandConstant(
            '{tmp}\DentalRay.SetupHelper'
        );


    HelperExe :=
        HelperDirectory +
        '\DentalRay.SetupHelper.exe';



    // --------------------------------------------------------
    // Check SetupHelper
    // --------------------------------------------------------

    if not FileExists(
        HelperExe
    ) then
    begin

        RaiseException(
            'DentalRay SetupHelper پیدا نشد.'
        );

    end;



    // --------------------------------------------------------
    // Run SetupHelper
    //
    // SetupHelper:
    //
    // - به SQL Server وصل می‌شود.
    // - Database را ایجاد / آماده می‌کند.
    // - Database Script را اجرا می‌کند.
    // - دسترسی NT AUTHORITY\SYSTEM را تنظیم می‌کند.
    //
    // --------------------------------------------------------

    if not RunHiddenAndWait(
        HelperExe,
        '--server "' +
        SqlServer +
        '"',
        HelperDirectory,
        ResultCode
    ) then
    begin

        RaiseException(
            'SetupHelper اجرا نشد.'
        );

    end;



    // --------------------------------------------------------
    // Check SetupHelper Exit Code
    // --------------------------------------------------------

    if ResultCode <> 0 then
    begin

        RaiseException(
            'آماده‌سازی دیتابیس انجام نشد.' +
            CRLF +
            'SetupHelper Exit Code: ' +
            IntToStr(
                ResultCode
            )
        );

    end;



    // ========================================================
    // 2. Radiology Storage
    // ========================================================

    if not ForceDirectories(
        StoragePath
    ) then
    begin

        RaiseException(
            'امکان ایجاد پوشه تصاویر وجود ندارد:' +
            CRLF +
            StoragePath
        );

    end;



    // ========================================================
    // 3. Radiology Storage Permission
    // ========================================================
    //
    // Full Control فقط برای:
    //
    // SYSTEM
    // Administrators
    //
    // SYSTEM:
    // Windows Service DentalRay
    //
    // Administrators:
    // مدیریت و Backup
    //
    // ========================================================

    if not RunHiddenAndWait(
        ExpandConstant(
            '{sys}\icacls.exe'
        ),
        '"' +
        StoragePath +
        '"' +
        ' /inheritance:r' +
        ' /grant:r' +
        ' "*S-1-5-18:(OI)(CI)F"' +
        ' "*S-1-5-32-544:(OI)(CI)F"',
        '',
        ResultCode
    ) then
    begin

        RaiseException(
            'امکان تنظیم دسترسی پوشه تصاویر وجود ندارد.'
        );

    end;



    // --------------------------------------------------------
    // Check icacls Exit Code
    // --------------------------------------------------------

    if ResultCode <> 0 then
    begin

        RaiseException(
            'تنظیم دسترسی پوشه تصاویر ناموفق بود.' +
            CRLF +
            'Exit Code: ' +
            IntToStr(
                ResultCode
            )
        );

    end;



    // ========================================================
    // 4. ProgramData Configuration Directory
    // ========================================================

    ConfigDirectory :=
        ExpandConstant(
            '{commonappdata}\DentalRay'
        );


    if not ForceDirectories(
        ConfigDirectory
    ) then
    begin

        RaiseException(
            'امکان ایجاد پوشه تنظیمات DentalRay وجود ندارد.'
        );

    end;



    ConfigFile :=
        ConfigDirectory +
        '\DentalRay.config.json';



    // ========================================================
    // 5. DentalRay.config.json
    // ========================================================
    //
    // Connection String از Windows Authentication استفاده
    // می‌کند.
    //
    // بنابراین:
    //
    // SQL Username ذخیره نمی‌شود.
    // SQL Password ذخیره نمی‌شود.
    //
    // Windows Service با LocalSystem اجرا می‌شود.
    //
    // SetupHelper دسترسی SQL را برای:
    //
    // NT AUTHORITY\SYSTEM
    //
    // ایجاد می‌کند.
    //
    // ========================================================

    ConfigText :=
        '{' + CRLF +

        '  "ConnectionStrings": {' +
        CRLF +

        '    "DentalRay": "' +
        'Server=' +
        JsonEscape(
            SqlServer
        ) +
        ';Database=DentalRay;' +
        'Integrated Security=True;' +
        'Encrypt=True;' +
        'TrustServerCertificate=True;"' +
        CRLF +

        '  },' +
        CRLF +

        '  "RadiologyStorage": {' +
        CRLF +

        '    "RootPath": "' +
        JsonEscape(
            StoragePath
        ) +
        '"' +
        CRLF +

        '  },' +
        CRLF +

        '  "Urls": "http://0.0.0.0:5202",' +
        CRLF +

        '  "RemoteAccess": {' +
        CRLF +

        '    "LocalScheme": "http",' +
        CRLF +

        '    "LocalPort": 5202,' +
        CRLF +

        '    "PublicHost": "",' +
        CRLF +

        '    "PublicScheme": "http",' +
        CRLF +

        '    "PublicPort": 5202' +
        CRLF +

        '  },' +
        CRLF +

        '  "Logging": {' +
        CRLF +

        '    "LogLevel": {' +
        CRLF +

        '      "Default": "Information",' +
        CRLF +

        '      "Microsoft.AspNetCore": "Warning"' +
        CRLF +

        '    }' +
        CRLF +

        '  },' +
        CRLF +

        '  "AllowedHosts": "*"' +
        CRLF +

        '}' +
        CRLF;



    // --------------------------------------------------------
    // Save Configuration
    // --------------------------------------------------------

    if not SaveStringToFile(
        ConfigFile,
        ConfigText,
        False
    ) then
    begin

        RaiseException(
            'فایل تنظیمات DentalRay ساخته نشد.'
        );

    end;



    // ========================================================
    // 6. Configuration File Permission
    // ========================================================
    //
    // فقط:
    //
    // LocalSystem
    // Administrators
    //
    // ========================================================

    if not RunHiddenAndWait(
        ExpandConstant(
            '{sys}\icacls.exe'
        ),
        '"' +
        ConfigFile +
        '"' +
        ' /inheritance:r' +
        ' /grant:r' +
        ' "*S-1-5-18:F"' +
        ' "*S-1-5-32-544:F"',
        '',
        ResultCode
    ) then
    begin

        RaiseException(
            'امکان تنظیم دسترسی فایل DentalRay.config.json وجود ندارد.'
        );

    end;



    // --------------------------------------------------------
    // Check icacls Exit Code
    // --------------------------------------------------------

    if ResultCode <> 0 then
    begin

        RaiseException(
            'تنظیم دسترسی فایل DentalRay.config.json ناموفق بود.' +
            CRLF +
            'Exit Code: ' +
            IntToStr(
                ResultCode
            )
        );

    end;



    // ========================================================
    // 7. Windows Service
    // ========================================================

    DentalRayExe :=
        ExpandConstant(
            '{app}\DentalRay.Api.exe'
        );



    // --------------------------------------------------------
    // Create Windows Service
    //
    // Startup Type:
    // Automatic
    //
    // --------------------------------------------------------

    if not RunHiddenAndWait(
        ExpandConstant(
            '{sys}\sc.exe'
        ),
        'create DentalRay ' +
        'binPath= "' +
        DentalRayExe +
        '" ' +
        'start= auto ' +
        'DisplayName= "DentalRay"',
        '',
        ResultCode
    ) then
    begin

        RaiseException(
            'امکان اجرای دستور ایجاد Windows Service وجود ندارد.'
        );

    end;



    if ResultCode <> 0 then
    begin

        RaiseException(
            'Windows Service DentalRay ایجاد نشد.' +
            CRLF +
            'Exit Code: ' +
            IntToStr(
                ResultCode
            )
        );

    end;



    // ========================================================
    // 8. Windows Service Description
    // ========================================================

    if not RunHiddenAndWait(
        ExpandConstant(
            '{sys}\sc.exe'
        ),
        'description DentalRay ' +
        '"DentalRay Dental Radiology Service"',
        '',
        ResultCode
    ) then
    begin

        RaiseException(
            'امکان تنظیم توضیحات Windows Service وجود ندارد.'
        );

    end;



    if ResultCode <> 0 then
    begin

        RaiseException(
            'توضیحات Windows Service تنظیم نشد.' +
            CRLF +
            'Exit Code: ' +
            IntToStr(
                ResultCode
            )
        );

    end;



    // ========================================================
    // 9. Start Windows Service
    // ========================================================

    if not RunHiddenAndWait(
        ExpandConstant(
            '{sys}\sc.exe'
        ),
        'start DentalRay',
        '',
        ResultCode
    ) then
    begin

        RaiseException(
            'امکان اجرای Windows Service وجود ندارد.'
        );

    end;



    if ResultCode <> 0 then
    begin

        RaiseException(
            'Windows Service DentalRay ایجاد شد ولی Start نشد.' +
            CRLF +
            'Exit Code: ' +
            IntToStr(
                ResultCode
            )
        );

    end;

end;



// ============================================================
// CurStepChanged
// ============================================================
//
// پس از کپی کامل فایل‌های برنامه، تنظیمات نهایی اجرا می‌شوند.
//
// ============================================================

procedure CurStepChanged(
    CurStep: TSetupStep
);

begin

    if CurStep =
        ssPostInstall then
    begin

        ConfigureDentalRay;

    end;

end;
