// ExplorerCommand.h
#pragma once

#include "stdafx.h"

// CLSID: {33560014-F9AA-43E9-83E3-3F58B9F03810}
extern const CLSID CLSID_DiskBenchExplorerCommand;

class ExplorerCommand : public IExplorerCommand
{
public:
    ExplorerCommand();
    ~ExplorerCommand();

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppv);
    STDMETHOD_(ULONG, AddRef)();
    STDMETHOD_(ULONG, Release)();

    // IExplorerCommand
    STDMETHOD(GetTitle)(IShellItemArray* psiItemArray, LPWSTR* ppszName);
    STDMETHOD(GetIcon)(IShellItemArray* psiItemArray, LPWSTR* ppszIcon);
    STDMETHOD(GetToolTip)(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip);
    STDMETHOD(GetCanonicalName)(GUID* pguidCommandName);
    STDMETHOD(GetState)(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState);
    STDMETHOD(Invoke)(IShellItemArray* psiItemArray, IBindCtx* pbc);
    STDMETHOD(GetFlags)(EXPCMDFLAGS* pFlags);
    STDMETHOD(EnumSubCommands)(IEnumExplorerCommand** ppEnum);

private:
    LONG m_refCount;

    HRESULT GetSelectedDrivePath(IShellItemArray* psiItemArray, LPWSTR pszPath, UINT cchPath);
    HRESULT GetExePath(LPWSTR pszExePath, UINT cchExePath);
    HRESULT ReadExePathFromRegistry(LPWSTR pszExePath, UINT cchExePath);
};

extern HINSTANCE g_hInstance;
