// Logger.cpp
#include "stdafx.h"
#include "Logger.h"

static void AppendLineUtf8(const wchar_t* line)
{
    if (line == nullptr)
        return;

    wchar_t path[MAX_PATH] = {};
    DWORD len = GetEnvironmentVariableW(L"LOCALAPPDATA", path, ARRAYSIZE(path));
    if (len == 0 || len >= ARRAYSIZE(path))
    {
        DWORD tmpLen = GetTempPathW(ARRAYSIZE(path), path);
        if (tmpLen == 0 || tmpLen >= ARRAYSIZE(path))
            return;
    }

    wchar_t filePath[MAX_PATH] = {};
    HRESULT hr = StringCchPrintfW(filePath, ARRAYSIZE(filePath), L"%s\\DiskBench\\ShellExtension.log", path);
    if (FAILED(hr))
        return;

    wchar_t dirPath[MAX_PATH] = {};
    hr = StringCchPrintfW(dirPath, ARRAYSIZE(dirPath), L"%s\\DiskBench", path);
    if (FAILED(hr))
        return;

    CreateDirectoryW(dirPath, nullptr);

    HANDLE hFile = CreateFileW(
        filePath,
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (hFile == INVALID_HANDLE_VALUE)
        return;

    char utf8[2048] = {};
    int utf8Len = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, ARRAYSIZE(utf8) - 3, nullptr, nullptr);
    if (utf8Len <= 0)
    {
        CloseHandle(hFile);
        return;
    }

    utf8[utf8Len - 1] = '\r';
    utf8[utf8Len] = '\n';
    utf8[utf8Len + 1] = '\0';

    DWORD bytesWritten = 0;
    WriteFile(hFile, utf8, (DWORD)(utf8Len + 1), &bytesWritten, nullptr);
    CloseHandle(hFile);
}

void LogMessage(const wchar_t* format, ...)
{
    if (format == nullptr)
        return;

    wchar_t buffer[1024] = {};
    va_list args;
    va_start(args, format);
    _vsnwprintf_s(buffer, ARRAYSIZE(buffer), _TRUNCATE, format, args);
    va_end(args);

    SYSTEMTIME st = {};
    GetLocalTime(&st);

    wchar_t line[1200] = {};
    HRESULT hr = StringCchPrintfW(
        line,
        ARRAYSIZE(line),
        L"%04u-%02u-%02u %02u:%02u:%02u.%03u [DiskBench.ShellExtension.Cpp] %s",
        st.wYear, st.wMonth, st.wDay,
        st.wHour, st.wMinute, st.wSecond, st.wMilliseconds,
        buffer);
    if (FAILED(hr))
        return;

    AppendLineUtf8(line);
}
