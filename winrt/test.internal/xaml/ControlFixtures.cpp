// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may
// not use these files except in compliance with the License. You may obtain
// a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations
// under the License.

#include "pch.h"
#include "ControlFixtures.h"
#include "MockDxgiSwapChain.h"

std::shared_ptr<CanvasAnimatedControlTestAdapter> CreateAnimatedControlTestAdapter(
    ComPtr<MockD2DDeviceContext> const& deviceContext)
{
    auto mockD2DDevice = Make<StubD2DDevice>();
    mockD2DDevice->MockCreateDeviceContext =
        [=](D2D1_DEVICE_CONTEXT_OPTIONS, ID2D1DeviceContext1** value)
        {
            ThrowIfFailed(deviceContext.CopyTo(value));
        };

    auto canvasDevice = Make<StubCanvasDevice>(mockD2DDevice);

    canvasDevice->CreateSwapChainMethod.AllowAnyCall(
        [=](
        int32_t widthInPixels,
        int32_t heightInPixels,
        DirectXPixelFormat format,
        int32_t bufferCount,
        CanvasAlphaMode alphaMode)
        {
            auto dxgiSwapChain = Make<MockDxgiSwapChain>();

            dxgiSwapChain->Present1Method.AllowAnyCall();
            dxgiSwapChain->SetMatrixTransformMethod.AllowAnyCall();
            
            dxgiSwapChain->GetDesc1Method.AllowAnyCall(
                [=](DXGI_SWAP_CHAIN_DESC1* desc)
                {
                    desc->Width = 1;
                    desc->Height = 1;
                    desc->Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                    desc->BufferCount = 2;
                    desc->AlphaMode = DXGI_ALPHA_MODE_IGNORE;
                    return S_OK;
                });

            dxgiSwapChain->GetBufferMethod.AllowAnyCall(
                [=](UINT index, const IID& iid, void** out)
                {
                    Assert::AreEqual(__uuidof(IDXGISurface2), iid);
                    auto surface = Make<MockDxgiSurface>();

                    return surface.CopyTo(reinterpret_cast<IDXGISurface2**>(out));
                });

            return dxgiSwapChain;
        });

    deviceContext->BeginDrawMethod.AllowAnyCall();

    deviceContext->ClearMethod.AllowAnyCall();

    deviceContext->SetDpiMethod.AllowAnyCall();

    deviceContext->EndDrawMethod.AllowAnyCall();

    deviceContext->SetTargetMethod.AllowAnyCall();
    
    deviceContext->CreateBitmapFromDxgiSurfaceMethod.AllowAnyCall(
        [](IDXGISurface* surface, const D2D1_BITMAP_PROPERTIES1* properties, ID2D1Bitmap1** out)
        {
            auto bitmap = Make<MockD2DBitmap>();

            return bitmap.CopyTo(out);
        });
    
    auto adapter = std::make_shared<CanvasAnimatedControlTestAdapter>(canvasDevice.Get());

    adapter->DeviceFactory->ActivateInstanceMethod.AllowAnyCall(
        [=](IInspectable** value)
        {
            return canvasDevice.CopyTo(value);
        });

    adapter->CreateCanvasSwapChainMethod.AllowAnyCall(
        [=](ICanvasDevice* device, float width, float height, float dpi, CanvasAlphaMode alphaMode)
        {
            auto swapChain = adapter->SwapChainManager->Create(
                device,
                1.0f,
                1.0f,
                DirectXPixelFormat::B8G8R8A8UIntNormalized,
                2,
                CanvasAlphaMode::Premultiplied,
                DEFAULT_DPI);

            return swapChain;
        });

    return adapter;
}
