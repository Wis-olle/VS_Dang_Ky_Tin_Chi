@echo off
setlocal
title Campus - Ban trinh dien
cd /d "%~dp0.."
pwsh -NoProfile -File "%~dp0..\scripts\Run-ShowcaseDemo.ps1"
if errorlevel 1 pause
