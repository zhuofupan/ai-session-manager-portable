Option Explicit

Dim shell, fso, scriptDir, ps1Path, argsPath, argsFile, command, i

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
ps1Path = fso.BuildPath(scriptDir, "ai-session-turn-ended-notify.ps1")
argsPath = fso.BuildPath(shell.ExpandEnvironmentStrings("%TEMP%"), "ai-session-turn-ended-notify-" & fso.GetTempName() & ".json")

Set argsFile = fso.CreateTextFile(argsPath, True, True)
argsFile.Write "["
For i = 0 To WScript.Arguments.Count - 1
    If i > 0 Then argsFile.Write ","
    argsFile.Write JsonString(WScript.Arguments(i))
Next
argsFile.Write "]"
argsFile.Close

command = "powershell.exe -NoProfile -STA -ExecutionPolicy RemoteSigned -File " & Q(ps1Path) & " -ArgFile " & Q(argsPath)

shell.Run command, 0, False

Function Q(value)
    Q = Chr(34) & Replace(CStr(value), Chr(34), Chr(34) & Chr(34)) & Chr(34)
End Function

Function JsonString(value)
    Dim text
    text = CStr(value)
    text = Replace(text, "\", "\\")
    text = Replace(text, Chr(34), "\""")
    text = Replace(text, vbCr, "\r")
    text = Replace(text, vbLf, "\n")
    text = Replace(text, vbTab, "\t")
    JsonString = Chr(34) & text & Chr(34)
End Function
