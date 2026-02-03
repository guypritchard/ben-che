// dllmain.cpp
#include "stdafx.h"
#include "ExplorerCommand.h"
#include "Logger.h"

HINSTANCE g_hInstance = nullptr;

// Class factory for ExplorerCommand
class ClassFactory : public IClassFactory
{
public:
    ClassFactory() : m_refCount(1) {}

    STDMETHOD(QueryInterface)(REFIID riid, void** ppv)
    {
        if (ppv == nullptr)
            return E_POINTER;

        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }

        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    STDMETHOD_(ULONG, AddRef)()
    {
        return InterlockedIncrement(&m_refCount);
    }

    STDMETHOD_(ULONG, Release)()
    {
        LONG refCount = InterlockedDecrement(&m_refCount);
        if (refCount == 0)
            delete this;
        return refCount;
    }

    STDMETHOD(CreateInstance)(IUnknown* pUnkOuter, REFIID riid, void** ppv)
    {
        if (ppv == nullptr)
            return E_POINTER;

        *ppv = nullptr;

        if (pUnkOuter != nullptr)
            return CLASS_E_NOAGGREGATION;

        ExplorerCommand* pCommand = new (std::nothrow) ExplorerCommand();
        if (pCommand == nullptr)
            return E_OUTOFMEMORY;

        HRESULT hr = pCommand->QueryInterface(riid, ppv);
        pCommand->Release();
        return hr;
    }

    STDMETHOD(LockServer)(BOOL fLock)
    {
        UNREFERENCED_PARAMETER(fLock);
        return S_OK;
    }

private:
    LONG m_refCount;
};

// DLL exports
HMODULE g_hModule = nullptr;

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    {
        wchar_t exePath[MAX_PATH] = {};
        GetModuleFileNameW(nullptr, exePath, ARRAYSIZE(exePath));
        LogMessage(L"DllGetClassObject pid=%lu exe=%s CLSID=%08lX", GetCurrentProcessId(), exePath, rclsid.Data1);
    }
    if (ppv == nullptr)
        return E_POINTER;

    *ppv = nullptr;

    if (rclsid != CLSID_DiskBenchExplorerCommand)
        return CLASS_E_CLASSNOTAVAILABLE;

    ClassFactory* pFactory = new (std::nothrow) ClassFactory();
    if (pFactory == nullptr)
        return E_OUTOFMEMORY;

    HRESULT hr = pFactory->QueryInterface(riid, ppv);
    pFactory->Release();
    LogMessage(L"DllGetClassObject returning hr=0x%08X", hr);
    return hr;
}

STDAPI DllCanUnloadNow()
{
    LogMessage(L"DllCanUnloadNow called");
    return S_OK;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    UNREFERENCED_PARAMETER(lpReserved);
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hInstance = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
