@echo off
setlocal
title Campus - Dang nhap
cd /d "%~dp0"
dotnet run --project "%~dp0QuanLyGiangVien.csproj" --launch-profile QuanLyGiangVien
if errorlevel 1 pause
