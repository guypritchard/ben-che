// ExplorerCommand.cpp
#include "stdafx.h"
#include "ExplorerCommand.h"
#include "Logger.h"

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "oleaut32.lib")

const CLSID CLSID_DiskBenchExplorerCommand =
    { 0x33560014, 0xf9aa, 0x43e9, { 0x83, 0xe3, 0x3f, 0x58, 0xb9, 0xf0, 0x38, 0x10 } };

const wchar_t* COMMAND_TITLE = L"Benchmark Drive Performance";
const wchar_t* COMMAND_TOOLTIP = L"Run DiskBench on this drive";
const wchar_t* CONFIG_REG_PATH = L"SOFTWARE\\DiskBench\\ShellExtension";

ExplorerCommand::ExplorerCommand() : m_refCount(1)
{
    OutputDebugString(L"ExplorerCommand constructed\n");
    LogMessage(L"ExplorerCommand ctor");
}

ExplorerCommand::~ExplorerCommand()
{
    OutputDebugString(L"ExplorerCommand destructed\n");
    LogMessage(L"ExplorerCommand dtor");
}

// IUnknown implementation
HRESULT STDMETHODCALLTYPE ExplorerCommand::QueryInterface(REFIID riid, void** ppv)
{
    if (ppv == nullptr)
        return E_POINTER;

    if (riid == IID_IUnknown || riid == IID_IExplorerCommand)
    {
        *ppv = static_cast<IExplorerCommand*>(this);
        AddRef();
        return S_OK;
    }

    *ppv = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE ExplorerCommand::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

ULONG STDMETHODCALLTYPE ExplorerCommand::Release()
{
    LONG refCount = InterlockedDecrement(&m_refCount);
    if (refCount == 0)
        delete this;
    return refCount;
}

// IExplorerCommand implementation
HRESULT STDMETHODCALLTYPE ExplorerCommand::GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName)
{
    OutputDebugString(L"GetTitle\n");

    UNREFERENCED_PARAMETER(psiItemArray);
    LogMessage(L"GetTitle");

    if (ppszName == nullptr)
        return E_POINTER;

    size_t len = wcslen(COMMAND_TITLE) + 1;
    *ppszName = (LPWSTR)CoTaskMemAlloc(len * sizeof(wchar_t));
    if (*ppszName == nullptr)
        return E_OUTOFMEMORY;

    StringCchCopy(*ppszName, len, COMMAND_TITLE);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon)
{
    OutputDebugString(L"GetIcon\n");

    UNREFERENCED_PARAMETER(psiItemArray);
    LogMessage(L"GetIcon");

    if (ppszIcon == nullptr)
        return E_POINTER;

    wchar_t exePath[MAX_PATH] = {};
    bool hasExePath = SUCCEEDED(GetExePath(exePath, ARRAYSIZE(exePath)));

    wchar_t dllPath[MAX_PATH] = {};
    bool hasDllPath = false;
    if (g_hInstance != nullptr)
    {
        DWORD len = GetModuleFileNameW(g_hInstance, dllPath, ARRAYSIZE(dllPath));
        hasDllPath = (len > 0 && len < ARRAYSIZE(dllPath));
    }

    const wchar_t* iconPath = hasExePath ? exePath : (hasDllPath ? dllPath : nullptr);
    if (iconPath != nullptr)
    {
        size_t len = wcslen(iconPath) + 10; // +10 for ",0\0"
        *ppszIcon = (LPWSTR)CoTaskMemAlloc(len * sizeof(wchar_t));
        if (*ppszIcon == nullptr)
            return E_OUTOFMEMORY;

        StringCchPrintf(*ppszIcon, len, L"%s,0", iconPath);
        LogMessage(L"GetIcon: %s", *ppszIcon);
        return S_OK;
    }

    OutputDebugString(L"GetIcon: ExePath not found\n");
    *ppszIcon = nullptr;
    return S_FALSE;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::GetToolTip(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip)
{
    OutputDebugString(L"GetToolTip\n");

    UNREFERENCED_PARAMETER(psiItemArray);
    LogMessage(L"GetToolTip");

    if (ppszInfotip == nullptr)
        return E_POINTER;

    size_t len = wcslen(COMMAND_TOOLTIP) + 1;
    *ppszInfotip = (LPWSTR)CoTaskMemAlloc(len * sizeof(wchar_t));
    if (*ppszInfotip == nullptr)
        return E_OUTOFMEMORY;

    StringCchCopy(*ppszInfotip, len, COMMAND_TOOLTIP);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::GetCanonicalName(GUID* pguidCommandName)
{
    OutputDebugString(L"GetCanonicalName\n");

    LogMessage(L"GetCanonicalName");
    if (pguidCommandName == nullptr)
        return E_POINTER;

    *pguidCommandName = CLSID_DiskBenchExplorerCommand;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState)
{
    OutputDebugString(L"GetState\n");

    UNREFERENCED_PARAMETER(fOkToBeSlow);
    LogMessage(L"GetState");

    if (pCmdState == nullptr)
        return E_POINTER;

    if (psiItemArray == nullptr)
    {
        *pCmdState = ECS_HIDDEN;
        return S_OK;
    }

    wchar_t drivePath[MAX_PATH] = {};
    HRESULT hr = GetSelectedDrivePath(psiItemArray, drivePath, ARRAYSIZE(drivePath));
    
    if (SUCCEEDED(hr) && wcslen(drivePath) > 0)
    {
        *pCmdState = ECS_ENABLED;
        OutputDebugString(L"GetState: ENABLED\n");
    }
    else
    {
        *pCmdState = ECS_HIDDEN;
        OutputDebugString(L"GetState: HIDDEN\n");
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc)
{
    OutputDebugString(L"Invoke\n");

    UNREFERENCED_PARAMETER(pbc);
    LogMessage(L"Invoke");

    if (psiItemArray == nullptr)
        return E_POINTER;

    wchar_t drivePath[MAX_PATH] = {};
    if (FAILED(GetSelectedDrivePath(psiItemArray, drivePath, ARRAYSIZE(drivePath))) || wcslen(drivePath) == 0)
    {
        OutputDebugString(L"Invoke: no drive selected\n");
        return S_FALSE;
    }

    wchar_t exePath[MAX_PATH] = {};
    if (FAILED(GetExePath(exePath, ARRAYSIZE(exePath))))
    {
        OutputDebugString(L"Invoke: ExePath not found\n");
        return E_FAIL;
    }

    wchar_t cmdLine[MAX_PATH * 2] = {};
    StringCchPrintf(cmdLine, ARRAYSIZE(cmdLine), L"\"%s\" --quick \"%s\"", exePath, drivePath);

    STARTUPINFO si = {};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi = {};

    OutputDebugString(L"Invoke: launching process\n");
    if (CreateProcess(exePath, cmdLine, nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi))
    {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return S_OK;
    }

    OutputDebugString(L"Invoke: CreateProcess failed\n");
    return E_FAIL;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::GetFlags(EXPCMDFLAGS* pFlags)
{
    OutputDebugString(L"GetFlags\n");

    LogMessage(L"GetFlags");
    if (pFlags == nullptr)
        return E_POINTER;

    *pFlags = ECF_DEFAULT;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE ExplorerCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    OutputDebugString(L"EnumSubCommands\n");

    LogMessage(L"EnumSubCommands");
    if (ppEnum == nullptr)
        return E_POINTER;

    *ppEnum = nullptr;
    return E_NOTIMPL;
}

// Helper methods
HRESULT ExplorerCommand::GetSelectedDrivePath(IShellItemArray* psiItemArray, LPWSTR pszPath, UINT cchPath)
{
    LogMessage(L"GetSelectedDrivePath");
    if (psiItemArray == nullptr || pszPath == nullptr || cchPath == 0)
        return E_INVALIDARG;

    DWORD count = 0;
    if (FAILED(psiItemArray->GetCount(&count)) || count == 0)
        return S_FALSE;

    IShellItem* psi = nullptr;
    if (FAILED(psiItemArray->GetItemAt(0, &psi)))
        return E_FAIL;

    LPWSTR pszName = nullptr;
    HRESULT hr = psi->GetDisplayName(SIGDN_FILESYSPATH, &pszName);
    
    if (SUCCEEDED(hr) && pszName != nullptr)
    {
        size_t nameLen = wcslen(pszName);
        if (nameLen >= 2 && pszName[1] == L':')
        {
            // Accept "C:" or "C:\" only (drive root)
            if (nameLen == 2 || (nameLen == 3 && (pszName[2] == L'\\' || pszName[2] == L'/')))
            {
                wchar_t drivePath[4] = { 0 };
                drivePath[0] = towupper(pszName[0]);
                drivePath[1] = L':';
                drivePath[2] = L'\\';
                drivePath[3] = L'\0';

                StringCchCopy(pszPath, cchPath, drivePath);
                CoTaskMemFree(pszName);
                psi->Release();
                LogMessage(L"GetSelectedDrivePath: %s", pszPath);
                return S_OK;
            }
        }

        CoTaskMemFree(pszName);
    }

    psi->Release();
    LogMessage(L"GetSelectedDrivePath: no drive");
    return S_FALSE;
}

HRESULT ExplorerCommand::GetExePath(LPWSTR pszExePath, UINT cchExePath)
{
    if (pszExePath == nullptr || cchExePath == 0)
        return E_INVALIDARG;

    return ReadExePathFromRegistry(pszExePath, cchExePath);
}

HRESULT ExplorerCommand::ReadExePathFromRegistry(LPWSTR pszExePath, UINT cchExePath)
{
    HKEY hKey = nullptr;
    LONG result = RegOpenKeyEx(HKEY_LOCAL_MACHINE, CONFIG_REG_PATH, 0, KEY_READ, &hKey);
    
    if (result != ERROR_SUCCESS)
    {
        OutputDebugString(L"ReadExePathFromRegistry: RegOpenKeyEx HKLM failed\n");
        LogMessage(L"ReadExePathFromRegistry: RegOpenKeyEx HKLM failed (%ld)", result);

        result = RegOpenKeyEx(HKEY_CURRENT_USER, CONFIG_REG_PATH, 0, KEY_READ, &hKey);
        if (result != ERROR_SUCCESS)
        {
            OutputDebugString(L"ReadExePathFromRegistry: RegOpenKeyEx HKCU failed\n");
            LogMessage(L"ReadExePathFromRegistry: RegOpenKeyEx HKCU failed (%ld)", result);
            return E_FAIL;
        }
    }

    wchar_t buffer[MAX_PATH] = {};
    DWORD bufferSize = sizeof(buffer);
    result = RegQueryValueEx(hKey, L"ExePath", nullptr, nullptr, (LPBYTE)buffer, &bufferSize);
    RegCloseKey(hKey);

    if (result != ERROR_SUCCESS)
    {
        OutputDebugString(L"ReadExePathFromRegistry: RegQueryValueEx failed\n");
        LogMessage(L"ReadExePathFromRegistry: RegQueryValueEx failed (%ld)", result);
        return E_FAIL;
    }

    StringCchCopy(pszExePath, cchExePath, buffer);
    LogMessage(L"ReadExePathFromRegistry: %s", pszExePath);
    return S_OK;
}
