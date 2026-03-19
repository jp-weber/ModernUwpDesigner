#include "pch.h"
#include <shellapi.h>
#include <mscoree.h>
#include <metahost.h>
#include <wrl.h>
#include <wil/result.h>
#include <wil/resource.h>
#include <memory>

using namespace Microsoft::WRL;

static bool g_bInitialized = false;

static DWORD WINAPI ThreadProc(LPVOID lpParameter)
{
    ComPtr<ICLRMetaHost> metaHost;
    RETURN_IF_FAILED(CLRCreateInstance(CLSID_CLRMetaHost, IID_PPV_ARGS(&metaHost)));

    ComPtr<ICLRRuntimeInfo> runtimeInfo;
    RETURN_IF_FAILED(metaHost->GetRuntime(L"v4.0.30319", IID_PPV_ARGS(&runtimeInfo)));

    ComPtr<ICLRRuntimeHost> runtimeHost;
    RETURN_IF_FAILED(runtimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_PPV_ARGS(&runtimeHost)));
    LOG_IF_FAILED(runtimeHost->Start());

    DWORD bufferSize = 0;
    RETURN_LAST_ERROR_IF((bufferSize = GetEnvironmentVariableW(L"MODERN_UWP_DESIGNER_DLL", nullptr, 0)) == 0);

    auto buffer = std::make_unique<wchar_t[]>(static_cast<size_t>(bufferSize) + 1);
    RETURN_LAST_ERROR_IF((bufferSize = GetEnvironmentVariableW(L"MODERN_UWP_DESIGNER_DLL", buffer.get(), bufferSize + 1)) == 0);

    auto args = L"";
    std::unique_ptr<wchar_t[]> argsBuffer;
    if ((bufferSize = GetEnvironmentVariableW(L"MODERN_UWP_DESIGNER_ARGS", nullptr, 0)) > 0)
    {
        argsBuffer = std::make_unique<wchar_t[]>(static_cast<size_t>(bufferSize) + 1);

        if (GetEnvironmentVariableW(L"MODERN_UWP_DESIGNER_ARGS", argsBuffer.get(), bufferSize + 1) > 0)
            args = argsBuffer.get();
    }

    DWORD returnValue = 0;
    RETURN_IF_FAILED(runtimeHost->ExecuteInDefaultAppDomain(buffer.get(), L"ModernUwpDesigner.BlendShim", L"Attach", args, &returnValue));
    RETURN_HR(static_cast<HRESULT>(returnValue));
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        if (!g_bInitialized)
        {
            g_bInitialized = true;
            CreateThread(nullptr, 0, ThreadProc, nullptr, 0, nullptr);
        }

        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }

    return TRUE;
}