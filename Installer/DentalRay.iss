#define MyAppName "DentalRay"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "Rahim Namazi"
#define MyAppExeName "DentalRay.Api.exe"


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
OutputBaseFilename=DentalRay_Setup_1.0.1

Compression=lzma2
SolidCompression=yes

WizardStyle=modern

UninstallDisplayIcon={app}\{#MyAppExeName}



[Languages]

Name: "english"; MessagesFile: "compiler:Default.isl"



[Files]

; Helper و SQL Script قبل از مرحله کپی اصلی نیز برای تشخیص و
; اعتبارسنجی SQL Server در {tmp} در دسترس قرار می‌گیرند.
Source: "..\Publish\DentalRay.SetupHelper\DentalRay.SetupHelper.exe"; \
    Flags: dontcopy noencryption

Source: "..\Publish\DentalRay.SetupHelper\Database\DentalRay.Database.Install.sql"; \
    Flags: dontcopy noencryption


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

Source: "..\Publish\DentalRay\*"; \
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

Source: "..\Publish\DentalRay.SetupHelper\*"; \
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



// RunHiddenAndWait در InitializeWizard استفاده می‌شود، بنابراین
// قبل از آن به صورت forward معرفی می‌شود.
function RunHiddenAndWait(
    FileName: String;
    Parameters: String;
    WorkingDirectory: String;
    var ResultCode: Integer
): Boolean; forward;



function ExtractJsonStringValue(
    JsonText: String;
    KeyName: String
): String;

var
    KeyPos:
        Integer;

    ColonPos:
        Integer;

    QuoteStart:
        Integer;

    QuoteEnd:
        Integer;

    I:
        Integer;

    Ch:
        Char;

    ValueText:
        String;

begin

    Result :=
        '';


    KeyPos :=
        Pos(
            '"' + KeyName + '"',
            JsonText
        );


    if KeyPos = 0 then
    begin

        Exit;

    end;


    ColonPos :=
        Pos(
            ':',
            Copy(
                JsonText,
                KeyPos,
                Length(JsonText) -
                KeyPos +
                1
            )
        );


    if ColonPos = 0 then
    begin

        Exit;

    end;


    ColonPos :=
        KeyPos +
        ColonPos -
        1;


    QuoteStart :=
        0;


    for I :=
        ColonPos + 1 to
        Length(JsonText) do
    begin

        if JsonText[I] = '"' then
        begin

            QuoteStart :=
                I + 1;

            Break;

        end;

    end;


    if QuoteStart = 0 then
    begin

        Exit;

    end;


    QuoteEnd :=
        0;

    I :=
        QuoteStart;


    while I <=
        Length(JsonText) do
    begin

        Ch :=
            JsonText[I];


        if Ch = '"' then
        begin

            if (I = QuoteStart) or
               (JsonText[I - 1] <> '\') then
            begin

                QuoteEnd :=
                    I - 1;

                Break;

            end;

        end;


        I :=
            I + 1;

    end;


    if QuoteEnd <
        QuoteStart then
    begin

        Exit;

    end;


    ValueText :=
        Copy(
            JsonText,
            QuoteStart,
            QuoteEnd -
            QuoteStart +
            1
        );


    StringChangeEx(
        ValueText,
        '\\',
        '\',
        True
    );


    StringChangeEx(
        ValueText,
        '\"',
        '"',
        True
    );


    Result :=
        ValueText;

end;



function DetectExistingRadiologyStoragePath(): String;

var
    ConfigFile:
        String;

    ConfigText:
        AnsiString;

    ExistingPath:
        String;

begin

    Result :=
        '';


    // اولویت اول:
    // اگر DentalRay قبلاً نصب شده باشد، مسیر واقعی ذخیره تصاویر
    // از فایل تنظیمات موجود خوانده می‌شود.
    ConfigFile :=
        ExpandConstant(
            '{commonappdata}\DentalRay\DentalRay.config.json'
        );


    if FileExists(
        ConfigFile
    ) then
    begin

        if LoadStringFromFile(
            ConfigFile,
            ConfigText
        ) then
        begin

            ExistingPath :=
                ExtractJsonStringValue(
                    String(ConfigText),
                    'RootPath'
                );


            if (
                Trim(
                    ExistingPath
                ) <> ''
            ) and
               DirExists(
                    ExistingPath
               ) then
            begin

                Result :=
                    ExistingPath;

                Exit;

            end;

        end;

    end;


    // اولویت دوم:
    // مسیر پیش‌فرض قدیمی را فقط اگر واقعاً وجود داشته باشد انتخاب کن.
    if DirExists(
        'D:\RadiologyData'
    ) then
    begin

        Result :=
            'D:\RadiologyData';

        Exit;

    end;


    // اولویت سوم:
    // اگر پوشه‌ای با نام RadiologyData روی چند درایو رایج وجود داشت،
    // همان مسیر موجود را پیشنهاد بده.
    if DirExists(
        'E:\RadiologyData'
    ) then
    begin

        Result :=
            'E:\RadiologyData';

        Exit;

    end;


    if DirExists(
        'F:\RadiologyData'
    ) then
    begin

        Result :=
            'F:\RadiologyData';

        Exit;

    end;

end;



// ============================================================
// TryNormalizeRadiologyStoragePath
// ============================================================
//
// مجوزهای پوشه تصاویر در مرحله نصب به صورت بازگشتی تنظیم می‌شوند.
// بنابراین انتخاب ریشه یک درایو (برای مثال D:\) خطرناک است و می‌تواند
// مجوز تمام فایل‌های آن درایو را تغییر دهد. فقط یک زیرپوشه محلی و کامل
// پذیرفته می‌شود و مسیر Canonical برای مراحل بعدی برگردانده می‌شود.
//
// ============================================================

function TryNormalizeRadiologyStoragePath(
    PathValue: String;
    var NormalizedPath: String
): Boolean;

var
    DriveName:
        String;

begin

    NormalizedPath :=
        RemoveBackslashUnlessRoot(
            ExpandFileName(
                Trim(
                    PathValue
                )
            )
        );


    DriveName :=
        ExtractFileDrive(
            NormalizedPath
        );


    // مسیر باید روی یک درایو محلی باشد و بعد از نام درایو، حداقل
    // یک پوشه داشته باشد. در نتیجه D:\ و مسیرهای UNC رد می‌شوند.
    Result :=
        (Length(DriveName) = 2) and
        (DriveName[2] = ':') and
        (Length(NormalizedPath) > 3);

end;



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

var
    ResultCode:
        Integer;

    DetectFile:
        String;

    DetectedServer:
        String;

begin

    // Helper را قبل از ورود کاربر به مراحل اصلی استخراج می‌کنیم
    // تا بتوانیم SQL Serverهای نصب‌شده را بررسی کنیم.
    ExtractTemporaryFile(
        'DentalRay.SetupHelper.exe'
    );

    ExtractTemporaryFile(
        'DentalRay.Database.Install.sql'
    );


    SqlPage :=
        CreateInputQueryPage(
            wpSelectDir,
            'SQL Server',
            'انتخاب SQL Server',
            'DentalRay ابتدا Instanceهای محلی SQL Server را بررسی می‌کند.' +
            CRLF +
            'در صورت نیاز می‌توانید مقدار شناسایی‌شده را تغییر دهید.'
        );


    SqlPage.Add(
        'SQL Server / Instance:',
        False
    );


    SqlPage.Values[0] :=
        '.\DENTALRAY';


    // --------------------------------------------------------
    // Detect local SQL Server instance
    // --------------------------------------------------------

    DetectFile :=
        ExpandConstant(
            '{tmp}\DentalRay.SqlDetect.ini'
        );


    if RunHiddenAndWait(
        ExpandConstant(
            '{tmp}\DentalRay.SetupHelper.exe'
        ),
        '--detect-output "' +
        DetectFile +
        '"',
        ExpandConstant('{tmp}'),
        ResultCode
    ) then
    begin

        DetectedServer :=
            GetIniString(
                'Sql',
                'Server',
                '',
                DetectFile
            );


        if Trim(
            DetectedServer
        ) <> '' then
        begin

            SqlPage.Values[0] :=
                Trim(
                    DetectedServer
                );

        end;

    end;


    StoragePage :=
        CreateInputDirPage(
            SqlPage.ID,
            'Radiology Storage',
            'محل ذخیره تصاویر رادیولوژی را مشخص کنید.',
            'تمام تصاویر بیماران در این پوشه نگهداری خواهند شد.',
            False,
            ''
        );


    StoragePage.Add(
        'Radiology images folder:'
    );


    StoragePage.Values[0] :=
        DetectExistingRadiologyStoragePath();


    if Trim(
        StoragePage.Values[0]
    ) = '' then
    begin

        StoragePage.Values[0] :=
            'D:\RadiologyData';

    end;

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

var
    ResultCode:
        Integer;

    SqlServer:
        String;

    NormalizedStoragePath:
        String;

begin

    Result :=
        True;


    if CurPageID =
        SqlPage.ID then
    begin

        SqlServer :=
            Trim(
                SqlPage.Values[0]
            );


        if SqlServer = '' then
        begin

            MsgBox(
                'SQL Server / Instance را وارد کنید.',
                mbError,
                MB_OK
            );

            Result :=
                False;

            Exit;

        end;


        // فقط وجود نام Instance کافی نیست؛ اتصال واقعی باید
        // قبل از اجازه ادامه نصب موفق باشد.
        if not RunHiddenAndWait(
            ExpandConstant(
                '{tmp}\DentalRay.SetupHelper.exe'
            ),
            '--probe --server "' +
            SqlServer +
            '"',
            ExpandConstant('{tmp}'),
            ResultCode
        ) or
           (ResultCode <> 0) then
        begin

            MsgBox(
                'اتصال به SQL Server برقرار نشد.' +
                CRLF +
                'نام Server/Instance و Windows Authentication را بررسی کنید.' +
                CRLF +
                'نصب تا برقراری اتصال ادامه پیدا نمی‌کند.',
                mbError,
                MB_OK
            );

            Result :=
                False;

            Exit;

        end;

    end;


    if CurPageID =
        StoragePage.ID then
    begin

        if Trim(
            StoragePage.Values[0]
        ) = '' then
        begin

            MsgBox(
                'مسیر ذخیره تصاویر را مشخص کنید.',
                mbError,
                MB_OK
            );

            Result :=
                False;

            Exit;

        end;


        if not TryNormalizeRadiologyStoragePath(
            StoragePage.Values[0],
            NormalizedStoragePath
        ) then
        begin

            MsgBox(
                'مسیر تصاویر باید یک زیرپوشه روی درایو محلی باشد.' +
                CRLF +
                'برای مثال: D:\RadiologyData' +
                CRLF +
                'انتخاب ریشه درایو یا مسیر شبکه مجاز نیست.',
                mbError,
                MB_OK
            );

            Result :=
                False;

            Exit;

        end;


        StoragePage.Values[0] :=
            NormalizedStoragePath;

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

    SqlServer:
        String;

begin

    Result :=
        '';


    SqlServer :=
        Trim(
            SqlPage.Values[0]
        );


    // Database باید قبل از ورود Setup به مرحله جایگزینی فایل‌ها
    // با موفقیت ایجاد/Upgrade شود.
    if not RunHiddenAndWait(
        ExpandConstant(
            '{tmp}\DentalRay.SetupHelper.exe'
        ),
        '--server "' +
        SqlServer +
        '" --script "' +
        ExpandConstant(
            '{tmp}\DentalRay.Database.Install.sql'
        ) +
        '"',
        ExpandConstant('{tmp}'),
        ResultCode
    ) then
    begin

        Result :=
            'امکان اجرای آماده‌سازی دیتابیس وجود ندارد.';

        Exit;

    end;


    if ResultCode <> 0 then
    begin

        Result :=
            'آماده‌سازی یا بروزرسانی دیتابیس انجام نشد.' +
            CRLF +
            'SetupHelper Exit Code: ' +
            IntToStr(
                ResultCode
            ) +
            CRLF +
            'نصب متوقف شد و فایل‌های برنامه جایگزین نشدند.';

        Exit;

    end;


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



    // Database قبلاً در PrepareToInstall با موفقیت Upgrade شده است.

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
        ' "*S-1-5-32-544:(OI)(CI)F"' +
        ' /T',
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

        '  "Urls": "http://localhost:5202",' +
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
