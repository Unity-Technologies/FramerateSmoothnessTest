#include <utility>

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include <d3d11.h>
#include <dxgi.h>
#include <wrl.h>

using Microsoft::WRL::ComPtr;

static ComPtr<IDXGIFactory> s_DXGIFactory;
static ComPtr<ID3D11Device> s_D3D11Device;

static ComPtr<IDXGIOutput> s_CachedOutput;

extern "C" __declspec(dllexport) HRESULT GetCurrentRefreshRate(int* outNumerator, int* outDenominator)
{
    auto monitor = MonitorFromWindow(GetActiveWindow(), MONITOR_DEFAULTTONEAREST);
    if (monitor == nullptr)
        MonitorFromWindow(GetActiveWindow(), MONITOR_DEFAULTTOPRIMARY);

    bool matches = true;
    if (s_CachedOutput != nullptr)
    {
        DXGI_OUTPUT_DESC outputDesc;
        auto hr = s_CachedOutput->GetDesc(&outputDesc);
        if (FAILED(hr))
            return hr;

        if (outputDesc.Monitor != monitor)
            s_CachedOutput = nullptr;
    }

    if (s_CachedOutput == nullptr)
    {
        if (s_D3D11Device == nullptr)
        {
            ComPtr<ID3D11Device> d3d11Device;
            D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0 };
            auto hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, featureLevels, ARRAYSIZE(featureLevels), D3D11_SDK_VERSION, &d3d11Device, nullptr, nullptr);
            if (FAILED(hr))
                return hr;

            ComPtr<IDXGIDevice> dxgiDevice;
            hr = d3d11Device.As(&dxgiDevice);
            if (FAILED(hr))
                return hr;

            ComPtr<IDXGIAdapter> dxgiAdapter;
            hr = dxgiDevice->GetAdapter(&dxgiAdapter);
            if (FAILED(hr))
                return hr;

            hr = dxgiAdapter->GetParent(__uuidof(s_DXGIFactory), &s_DXGIFactory);
            if (FAILED(hr))
                return hr;

            s_D3D11Device = std::move(d3d11Device);
        }

        HRESULT hr = DXGI_ERROR_NOT_FOUND;
        ComPtr<IDXGIAdapter> adapter;

        for (UINT i = 0; s_CachedOutput == nullptr && SUCCEEDED(hr = s_DXGIFactory->EnumAdapters(i, &adapter)); i++)
        {
            ComPtr<IDXGIOutput> output;
            for (UINT j = 0; SUCCEEDED(hr = adapter->EnumOutputs(j, &output)); j++)
            {
                DXGI_OUTPUT_DESC outputDesc;
                hr = output->GetDesc(&outputDesc);
                if (FAILED(hr))
                    continue;

                if (outputDesc.Monitor == monitor)
                {
                    s_CachedOutput = std::move(output);
                    break;
                }
                
            }
        }

        if (s_CachedOutput == nullptr)
        {
            if (FAILED(hr))
                return hr;

            return DXGI_ERROR_NOT_FOUND;
        }
    }

    DXGI_MODE_DESC modeToMatch = {};
    DXGI_MODE_DESC currentMode;
    auto hr = s_CachedOutput->FindClosestMatchingMode(&modeToMatch, &currentMode, s_D3D11Device.Get());
    if (FAILED(hr))
        return hr;

    *outNumerator = currentMode.RefreshRate.Numerator;
    *outDenominator = currentMode.RefreshRate.Denominator;
    return S_OK;
}