@echo off
rem Runs Battler. "play.bat --editor" opens the editor instead.
set GODOT=C:\Users\calvi\Downloads\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe
start "" "%GODOT%" --path "%~dp0." %*
