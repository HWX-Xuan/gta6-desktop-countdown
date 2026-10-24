$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$source = Join-Path $root "dist\GTA6Countdown"
$install = Join-Path $env:LOCALAPPDATA "Programs\GTA6Countdown"

if (-not (Test-Path (Join-Path $source "GTA6Countdown.dll"))) {
    throw "Publish output not found. Run dotnet publish first."
}

# Stop only our own running copies.
Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -match "GTA6Countdown\.(exe|dll)" } |
    ForEach-Object {
        try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { }
    }
Start-Sleep -Milliseconds 700

New-Item -ItemType Directory -Force -Path $install | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $install -Recurse -Force

$wscript = Join-Path $env:WINDIR "System32\wscript.exe"
$launcher = Join-Path $install "launch.vbs"
$icon = Join-Path $install "assets\app.ico"

# Unicode-safe shortcut name, independent of this script's file encoding.
$shortcutName = "GTA VI " + [char]0x5012 + [char]0x8BA1 + [char]0x65F6 + ".lnk"
$desktop = [Environment]::GetFolderPath("Desktop")
$programs = [Environment]::GetFolderPath("Programs")
$desktopShortcutPath = Join-Path $desktop $shortcutName
$startMenuShortcutPath = Join-Path $programs $shortcutName

$shell = New-Object -ComObject WScript.Shell
foreach ($target in @($desktopShortcutPath, $startMenuShortcutPath)) {
    $shortcut = $shell.CreateShortcut($target)
    $shortcut.TargetPath = $wscript
    $shortcut.Arguments = "`"$launcher`""
    $shortcut.WorkingDirectory = $install
    $shortcut.IconLocation = "$icon,0"
    $shortcut.Description = "GTA VI countdown to 2026.11.18 19:00"
    $shortcut.Save()
}

# Give the taskbar a custom identity instead of ".NET Host". The shortcut is
# edited in a temporary copy because some sandboxed environments block direct
# property writes to the Desktop/Start Menu folders.
$aumidSource = @'
using System;
using System.IO;
using System.Runtime.InteropServices;

public static class Gta6ShortcutIdentity
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public IntPtr PointerValue;

        public static PropVariant FromString(string value)
        {
            PropVariant result = new PropVariant();
            result.VariantType = 31;
            result.PointerValue = Marshal.StringToCoTaskMemUni(value);
            return result;
        }

        public void Clear()
        {
            if (PointerValue != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(PointerValue);
                PointerValue = IntPtr.Zero;
            }
        }
    }

    public static void SetAumid(string shortcutPath, string appUserModelId)
    {
        object shellLink = null;
        try
        {
            shellLink = new ShellLink();
            IPersistFile persistFile = (IPersistFile)shellLink;
            persistFile.Load(shortcutPath, 2);
            IPropertyStore store = (IPropertyStore)shellLink;
            PropertyKey key = new PropertyKey();
            key.FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
            key.PropertyId = 5;
            PropVariant value = PropVariant.FromString(appUserModelId);
            try
            {
                store.SetValue(ref key, ref value);
                store.Commit();
            }
            finally
            {
                value.Clear();
            }
            persistFile.Save(shortcutPath, true);
        }
        finally
        {
            if (shellLink != null) Marshal.FinalReleaseComObject(shellLink);
        }
    }
}
'@

Add-Type -TypeDefinition $aumidSource -Language CSharp

foreach ($shortcutPath in @($desktopShortcutPath, $startMenuShortcutPath)) {
    $temp = Join-Path $env:TEMP ("gta6-shortcut-" + [Guid]::NewGuid().ToString("N") + ".lnk")
    Copy-Item -LiteralPath $shortcutPath -Destination $temp -Force
    [Gta6ShortcutIdentity]::SetAumid($temp, "GTA6.Countdown.Desktop.v1")
    Copy-Item -LiteralPath $temp -Destination $shortcutPath -Force
    Remove-Item -LiteralPath $temp -Force
}

# Launch through the shortcut so Explorer associates the AUMID with it.
Start-Process -FilePath $desktopShortcutPath

Write-Host ""
Write-Host "GTA VI countdown installed to:" -ForegroundColor Magenta
Write-Host "  $install"
Write-Host "Desktop shortcut created."
Write-Host "Right-click the tray icon to refresh, hide desktop icons, enable autostart, or exit."
