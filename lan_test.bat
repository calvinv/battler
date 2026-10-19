@echo off
rem Two copies of Battler side by side on this PC, playing each other over LAN (localhost).
rem Add "--auto" to have an AI play both sides, e.g. "lan_test.bat --auto".
set GODOT=C:\Users\calvi\Downloads\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe
start "" "%GODOT%" --path "%~dp0." --resolution 950x560 --position 0,40 -- --host %*
start "" "%GODOT%" --path "%~dp0." --resolution 950x560 --position 960,40 -- --join 127.0.0.1 %*
