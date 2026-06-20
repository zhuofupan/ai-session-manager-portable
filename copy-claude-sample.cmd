@echo off
REM ============================================================
REM  Copy Claude Code session transcripts to a folder Claude
REM  (Cowork) can read, so it can build the Claude Code reader.
REM  Source: %USERPROFILE%\.claude\projects  (transcripts only)
REM  Target: D:\AI\claude-projects-sample
REM  No credentials are copied. Safe to delete the target later.
REM ============================================================
setlocal
set "SRC=%USERPROFILE%\.claude\projects"
set "DST=D:\AI\claude-projects-sample"

echo Source: %SRC%
echo Target: %DST%
echo.

if not exist "%SRC%" (
  echo [!] Could not find "%SRC%".
  echo     Open a Claude Code session at least once, then re-run.
  pause
  exit /b 1
)

robocopy "%SRC%" "%DST%" *.jsonl /E /NFL /NDL /NJH /NP
echo.
echo Done. Copied .jsonl transcripts to "%DST%".
echo You can now tell Claude to continue.
pause
endlocal
