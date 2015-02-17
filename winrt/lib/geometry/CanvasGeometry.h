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

#pragma once

#include <Canvas.abi.h>

namespace ABI { namespace Microsoft { namespace Graphics { namespace Canvas
{
    using namespace ::Microsoft::WRL;
    using namespace Numerics;

    class CanvasGeometry;
    class CanvasGeometryManager;

    struct CanvasGeometryTraits
    {
        typedef ID2D1Geometry resource_t;
        typedef CanvasGeometry wrapper_t;
        typedef ICanvasGeometry wrapper_interface_t;
        typedef CanvasGeometryManager manager_t;
    };

    class CanvasGeometry : RESOURCE_WRAPPER_RUNTIME_CLASS(
        CanvasGeometryTraits,
        IClosable)
    {
        InspectableClass(RuntimeClass_Microsoft_Graphics_Canvas_CanvasGeometry, BaseTrust);

        ComPtr<ICanvasDevice> m_canvasDevice;

    public:
        CanvasGeometry(
            std::shared_ptr<CanvasGeometryManager> manager,
            ID2D1Geometry* d2dGeometry,
            ICanvasDevice* canvasDevice);

        IFACEMETHOD(Close)();

        IFACEMETHOD(get_Device)(ICanvasDevice** device);
    };

    class CanvasGeometryManager : public ResourceManager<CanvasGeometryTraits>
    {
    public:
        ComPtr<CanvasGeometry> CreateNew(
            ICanvasResourceCreator* device,
            Rect rect);

        ComPtr<CanvasGeometry> CreateNew(
            ICanvasResourceCreator* device,
            Vector2 center,
            float radiusX,
            float radiusY);

        ComPtr<CanvasGeometry> CreateNew(
            ICanvasResourceCreator* device,
            Rect rect,
            float radiusX,
            float radiusY);

        ComPtr<CanvasGeometry> CreateNew(
            ICanvasPathBuilder* pathBuilder);

        virtual ComPtr<CanvasGeometry> CreateWrapper(
            ICanvasDevice* device,
            ID2D1Geometry* resource);

    };

    class CanvasGeometryFactory
        : public ActivationFactory<
            ICanvasGeometryStatics,
            CloakedIid<ICanvasDeviceResourceFactoryNative>> ,
            public PerApplicationManager<CanvasGeometryFactory, CanvasGeometryManager>
    {
        InspectableClassStatic(RuntimeClass_Microsoft_Graphics_Canvas_CanvasGeometry, BaseTrust);

    public:
        IMPLEMENT_DEFAULT_ICANVASDEVICERESOURCEFACTORYNATIVE();

        IFACEMETHOD(CreateRectangle)(
            ICanvasResourceCreator* resourceCreator,
            Rect rect,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateRectangleAtCoords)(
            ICanvasResourceCreator* resourceCreator,
            float x,
            float y,
            float w,
            float h,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateRoundedRectangle)(
            ICanvasResourceCreator* resourceCreator,
            Rect rect,
            float radiusX,
            float radiusY,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateRoundedRectangleAtCoords)(
            ICanvasResourceCreator* resourceCreator,
            float x,
            float y,
            float w,
            float h,
            float radiusX,
            float radiusY,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateEllipse)(
            ICanvasResourceCreator* resourceCreator,
            Numerics::Vector2 centerPoint,
            float radiusX,
            float radiusY,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateEllipseAtCoords)(
            ICanvasResourceCreator* resourceCreator,
            float x,
            float y,
            float radiusX,
            float radiusY,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateCircle)(
            ICanvasResourceCreator* resourceCreator,
            Numerics::Vector2 centerPoint,
            float radius,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreateCircleAtCoords)(
            ICanvasResourceCreator* resourceCreator,
            float x,
            float y,
            float radius,
            ICanvasGeometry** geometry) override;

        IFACEMETHOD(CreatePath)(
            ICanvasPathBuilder* pathBuilder,
            ICanvasGeometry** geometry) override;
    };
}}}}
