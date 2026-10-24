' Launch GTA VI countdown with no console window.
Option Explicit

Dim shell, fso, baseDir, dllPath, dotnetPath, command
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

baseDir = fso.GetParentFolderName(WScript.ScriptFullName)
dllPath = baseDir & "\GTA6Countdown.dll"
dotnetPath = shell.ExpandEnvironmentStrings("%ProgramFiles%\dotnet\dotnet.exe")

If Not fso.FileExists(dotnetPath) Then
  dotnetPath = "dotnet.exe"
End If

shell.CurrentDirectory = baseDir
command = """" & dotnetPath & """ """ & dllPath & """"
shell.Run command, 0, False
