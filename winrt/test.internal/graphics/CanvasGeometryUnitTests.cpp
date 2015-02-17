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
#include "MockD2DRectangleGeometry.h"
#include "MockD2DEllipseGeometry.h"
#include "MockD2DRoundedRectangleGeometry.h"
#include "MockD2DPathGeometry.h"
#include "MockD2DGeometrySink.h"

TEST_CLASS(CanvasGeometryTests)
{
public: 

    struct Fixture
    {
        ComPtr<StubCanvasDevice> Device;
        std::shared_ptr<CanvasGeometryManager> Manager;

        Fixture()
            : Device(Make<StubCanvasDevice>())
            , Manager(std::make_shared<CanvasGeometryManager>())
        {
            Device->CreateRectangleGeometryMethod.AllowAnyCall(
                [](D2D1_RECT_F const&)
                {
                    return Make<MockD2DRectangleGeometry>();
                });

        }
    };

    TEST_METHOD_EX(CanvasGeometry_ImplementsExpectedInterfaces)
    {
        Fixture f;

        auto canvasGeometry = f.Manager->Create(f.Device.Get(), Rect{});

        ASSERT_IMPLEMENTS_INTERFACE(canvasGeometry, ICanvasGeometry);
        ASSERT_IMPLEMENTS_INTERFACE(canvasGeometry, ABI::Windows::Foundation::IClosable);
    }

    TEST_METHOD_EX(CanvasGeometry_CreatedWithCorrectD2DRectangleResource)
    {
        Fixture f;

        Rect expectedRect{ 1, 2, 3, 4 };
        const D2D1_RECT_F expectedD2DRect = 
            D2D1::RectF(expectedRect.X, expectedRect.Y, expectedRect.X + expectedRect.Width, expectedRect.Y + expectedRect.Height);
        
        f.Device->CreateRectangleGeometryMethod.SetExpectedCalls(1, 
            [expectedD2DRect](D2D1_RECT_F const& rect)
            {
                Assert::AreEqual(expectedD2DRect, rect);
                return Make<MockD2DRectangleGeometry>();
            });

        f.Manager->Create(f.Device.Get(), expectedRect);
    }

    TEST_METHOD_EX(CanvasGeometry_CreatedWithCorrectD2DEllipseResource)
    {
        Fixture f;

        D2D1_ELLIPSE testEllipse = D2D1::Ellipse(D2D1::Point2F(1, 2), 3, 4);

        f.Device->CreateEllipseGeometryMethod.SetExpectedCalls(1,
            [testEllipse](D2D1_ELLIPSE const& ellipse)
            {
                Assert::AreEqual(testEllipse, ellipse);
                return Make<MockD2DEllipseGeometry>();
            });
            
        f.Manager->Create(f.Device.Get(), Vector2{ testEllipse.point.x, testEllipse.point.y }, testEllipse.radiusX, testEllipse.radiusY);
    }

    TEST_METHOD_EX(CanvasGeometry_CreatedWithCorrectD2DRoundedRectangleResource)
    {
        Fixture f;

        Rect expectedRect{ 1, 2, 3, 4 };
        const D2D1_RECT_F expectedD2DRect =
            D2D1::RectF(expectedRect.X, expectedRect.Y, expectedRect.X + expectedRect.Width, expectedRect.Y + expectedRect.Height);
        float expectedRadiusX = 5;
        float expectedRadiusY = 6;

        f.Device->CreateRoundedRectangleGeometryMethod.SetExpectedCalls(1,
            [expectedD2DRect, expectedRadiusX, expectedRadiusY](D2D1_ROUNDED_RECT const& roundedRect)
            {
                Assert::AreEqual(D2D1::RoundedRect(expectedD2DRect, expectedRadiusX, expectedRadiusY), roundedRect);
                return Make<MockD2DRoundedRectangleGeometry>();
            });

        f.Manager->Create(f.Device.Get(), expectedRect, expectedRadiusX, expectedRadiusY);
    }

};

TEST_CLASS(CanvasPathBuilderUnitTests)
{
    struct SetupFixture
    {
        ComPtr<StubCanvasDevice> Device;
        std::shared_ptr<CanvasGeometryManager> Manager;

        SetupFixture()
            : Device(Make<StubCanvasDevice>())
            , Manager(std::make_shared<CanvasGeometryManager>())
        {
            Device->CreatePathGeometryMethod.AllowAnyCall(
                []()
                {
                    auto pathGeometry = Make<MockD2DPathGeometry>();

                    pathGeometry->OpenMethod.AllowAnyCall(
                        [](ID2D1GeometrySink** out)
                        {
                            auto geometrySink = Make<MockD2DGeometrySink>();                            

                            geometrySink->BeginFigureMethod.AllowAnyCall();
                            geometrySink->EndFigureMethod.AllowAnyCall();
                            geometrySink->CloseMethod.AllowAnyCall();

                            return geometrySink.CopyTo(out);
                        });

                    return pathGeometry;
                });
        }
    };

    TEST_METHOD_EX(CanvasPathBuilder_Closure)
    {
        SetupFixture f;

        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        Assert::AreEqual(S_OK, canvasPathBuilder->Close());

        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->BeginFigure(Vector2{}));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->BeginFigureWithFigureFill(Vector2{}, CanvasFigureFill::DoesNotAffectFills));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->AddArc(Vector2{}, 0, 0, 0, CanvasSweepDirection::Clockwise, CanvasArcSize::Small));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->AddCubicBezier(Vector2{}, Vector2{}, Vector2{}));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->AddLine(Vector2{}));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->AddLineWithCoords(0, 0));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->AddQuadraticBezier(Vector2{}, Vector2{}));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->SetSegmentOptions(CanvasFigureSegmentOptions::None));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->SetFilledRegionDetermination(CanvasFilledRegionDetermination::Alternate));
        Assert::AreEqual(RO_E_CLOSED, canvasPathBuilder->EndFigure(CanvasFigureLoop::Closed));

        // Verify that path builder's device was closed, as well.
        auto pathBuilderInternal = As<ICanvasPathBuilderInternal>(canvasPathBuilder);
        ExpectHResultException(RO_E_CLOSED, [&]{ pathBuilderInternal->GetDevice(); });
    }

    TEST_METHOD_EX(CanvasPathBuilder_CreationOpensGeometrySink)
    {
        SetupFixture f;
        
        f.Device->CreatePathGeometryMethod.SetExpectedCalls(1,
            []()
            {
                auto pathGeometry = Make<MockD2DPathGeometry>();

                pathGeometry->OpenMethod.SetExpectedCalls(1,
                    [](ID2D1GeometrySink** out)
                    {
                        auto geometrySink = Make<MockD2DGeometrySink>();
                        return geometrySink.CopyTo(out);
                    });

                return pathGeometry;
            });

        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());
    }

    TEST_METHOD_EX(CanvasPathBuilder_CanOnlyCreateOneGeometry)
    {
        SetupFixture f;

        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        f.Manager->Create(canvasPathBuilder.Get());

        ExpectHResultException(RO_E_CLOSED, [&]{ f.Manager->Create(canvasPathBuilder.Get()); });
    }

    TEST_METHOD_EX(CanvasPathBuilder_CreatePathClosesSink)
    {
        SetupFixture f;

        auto geometrySink = Make<MockD2DGeometrySink>();
        geometrySink->CloseMethod.SetExpectedCalls(1);

        auto pathGeometry = Make<MockD2DPathGeometry>();
        pathGeometry->OpenMethod.AllowAnyCall(
            [=](ID2D1GeometrySink** out)
            {
                return geometrySink.CopyTo(out);
            });
        
        f.Device->CreatePathGeometryMethod.AllowAnyCall(
            [=]()
            {
                return pathGeometry;
            });

        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        f.Manager->Create(canvasPathBuilder.Get());
    }

    struct SinkAccessFixture : SetupFixture
    {
        ComPtr<MockD2DGeometrySink> GeometrySink;
        ComPtr<CanvasPathBuilder> PathBuilder;

        SinkAccessFixture()
        {
            auto pathGeometry = Make<MockD2DPathGeometry>();
            GeometrySink = Make<MockD2DGeometrySink>();

            Device->CreatePathGeometryMethod.AllowAnyCall([=]() { return pathGeometry; });
            pathGeometry->OpenMethod.AllowAnyCall([=](ID2D1GeometrySink** out) { return GeometrySink.CopyTo(out); });

            PathBuilder = Make<CanvasPathBuilder>(Device.Get());

            GeometrySink->BeginFigureMethod.AllowAnyCall();
        }

    };

    TEST_METHOD_EX(CanvasPathBuilder_BeginFigure)
    {
        SinkAccessFixture f;
        
        f.GeometrySink->BeginFigureMethod.SetExpectedCalls(1, 
            [](D2D1_POINT_2F point, D2D1_FIGURE_BEGIN figureBegin)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), point);
                Assert::AreEqual(D2D1_FIGURE_BEGIN_FILLED, figureBegin);
            });
        ThrowIfFailed(f.PathBuilder->BeginFigure(Vector2{ 1, 2 }));
    }

    TEST_METHOD_EX(CanvasPathBuilder_BeginFigureWithFigureFill)
    {
        SinkAccessFixture f;

        f.GeometrySink->BeginFigureMethod.SetExpectedCalls(1,
            [](D2D1_POINT_2F point, D2D1_FIGURE_BEGIN figureBegin)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), point);
                Assert::AreEqual(D2D1_FIGURE_BEGIN_HOLLOW, figureBegin);
            });
        ThrowIfFailed(f.PathBuilder->BeginFigureWithFigureFill(Vector2{ 1, 2 }, CanvasFigureFill::DoesNotAffectFills));
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddArc)
    {
        SinkAccessFixture f;

        f.PathBuilder->BeginFigure(Vector2{});

        f.GeometrySink->AddArcMethod.SetExpectedCalls(1,
            [](CONST D2D1_ARC_SEGMENT* arc)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), arc->point);
                Assert::AreEqual(D2D1::SizeF(3, 4), arc->size);
                Assert::AreEqual(0.0f, arc->rotationAngle);
                Assert::AreEqual(D2D1_SWEEP_DIRECTION_CLOCKWISE, arc->sweepDirection);
                Assert::AreEqual(D2D1_ARC_SIZE_LARGE, arc->arcSize);
            });
        ThrowIfFailed(f.PathBuilder->AddArc(Vector2{ 1, 2 }, 3.0f, 4.0f, 0.0f, CanvasSweepDirection::Clockwise, CanvasArcSize::Large));
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddArc_ExpectsRadians)
    {
        const float pi = 3.14159f;
        float angles[] = { 0, pi / 2, pi, 3 * pi / 2, 2 * pi };

        for(float angle : angles)
        {
            SinkAccessFixture f;

            f.PathBuilder->BeginFigure(Vector2{});

            f.GeometrySink->AddArcMethod.SetExpectedCalls(1,
                [=](CONST D2D1_ARC_SEGMENT* arc)
                {
                    const float d2dDegrees = arc->rotationAngle;
                    const float radians = d2dDegrees  * pi / 180.0f;
                    Assert::AreEqual(angle, radians, 0.0001f);
                });
            ThrowIfFailed(f.PathBuilder->AddArc(Vector2{}, 0, 0, angle, CanvasSweepDirection::Clockwise, CanvasArcSize::Large));
        }
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddArc_InvalidState)
    {
        SinkAccessFixture f;

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->AddArc(Vector2{}, 0, 0, 0, CanvasSweepDirection::Clockwise, CanvasArcSize::Large));

        ValidateStoredErrorState(E_INVALIDARG, Strings::CanOnlyAddPathDataWhileInFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddCubicBezier)
    {
        SinkAccessFixture f;

        f.PathBuilder->BeginFigure(Vector2{});

        f.GeometrySink->AddBezierMethod.SetExpectedCalls(1,
            [](const D2D1_BEZIER_SEGMENT* segment)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), segment->point1);
                Assert::AreEqual(D2D1::Point2F(3, 4), segment->point2);
                Assert::AreEqual(D2D1::Point2F(5, 6), segment->point3);
            });
        ThrowIfFailed(f.PathBuilder->AddCubicBezier(Vector2{ 1, 2 }, Vector2{ 3, 4 }, Vector2{ 5, 6 }));
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddCubicBezier_InvalidState)
    {
        SinkAccessFixture f;

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->AddCubicBezier(Vector2{}, Vector2{}, Vector2{}));

        ValidateStoredErrorState(E_INVALIDARG, Strings::CanOnlyAddPathDataWhileInFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddLine)
    {
        SinkAccessFixture f;

        f.PathBuilder->BeginFigure(Vector2{});
        
        f.GeometrySink->AddLineMethod.SetExpectedCalls(1,
            [](D2D1_POINT_2F point)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), point);
            });
        ThrowIfFailed(f.PathBuilder->AddLine(Vector2{ 1, 2 }));
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddLine_InvalidState)
    {
        SinkAccessFixture f;

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->AddLine(Vector2{}));

        ValidateStoredErrorState(E_INVALIDARG, Strings::CanOnlyAddPathDataWhileInFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddLineWithCoords)
    {
        SinkAccessFixture f;

        f.PathBuilder->BeginFigure(Vector2{});
        
        f.GeometrySink->AddLineMethod.SetExpectedCalls(1,
            [](D2D1_POINT_2F point)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), point);
            });
        ThrowIfFailed(f.PathBuilder->AddLineWithCoords(1, 2));
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddLineWithCoords_InvalidState)
    {
        SinkAccessFixture f;

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->AddLineWithCoords(0, 0));

        ValidateStoredErrorState(E_INVALIDARG, Strings::CanOnlyAddPathDataWhileInFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddQuadraticBezier)
    {
        SinkAccessFixture f;

        f.PathBuilder->BeginFigure(Vector2{});
        
        f.GeometrySink->AddQuadraticBezierMethod.SetExpectedCalls(1,
            [](const D2D1_QUADRATIC_BEZIER_SEGMENT* segment)
            {
                Assert::AreEqual(D2D1::Point2F(1, 2), segment->point1);
                Assert::AreEqual(D2D1::Point2F(3, 4), segment->point2);
            });
        ThrowIfFailed(f.PathBuilder->AddQuadraticBezier(Vector2{ 1, 2 }, Vector2{ 3, 4 }));
    }

    TEST_METHOD_EX(CanvasPathBuilder_AddQuadraticBezier_InvalidState)
    {
        SinkAccessFixture f;

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->AddQuadraticBezier(Vector2{}, Vector2{}));

        ValidateStoredErrorState(E_INVALIDARG, Strings::CanOnlyAddPathDataWhileInFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_SetSegmentOptions)
    {
        SinkAccessFixture f;
        
        f.GeometrySink->SetSegmentFlagsMethod.SetExpectedCalls(1,
            [](D2D1_PATH_SEGMENT pathSegment)
            {
                Assert::AreEqual(D2D1_PATH_SEGMENT_FORCE_ROUND_LINE_JOIN, pathSegment);
            });
        ThrowIfFailed(f.PathBuilder->SetSegmentOptions(CanvasFigureSegmentOptions::ForceRoundLineJoin));
    }

    TEST_METHOD_EX(CanvasPathBuilder_SetFilledRegionDetermination)
    {
        SinkAccessFixture f;
        
        f.GeometrySink->SetFillModeMethod.SetExpectedCalls(1,
            [](D2D1_FILL_MODE fillMode)
            {
                Assert::AreEqual(D2D1_FILL_MODE_WINDING, fillMode);
            });
        ThrowIfFailed(f.PathBuilder->SetFilledRegionDetermination(CanvasFilledRegionDetermination::Winding));
    }

    TEST_METHOD_EX(CanvasPathBuilder_SetFilledRegionDetermination_InvalidAfterBeginFigure)
    {
        SinkAccessFixture f;

        f.PathBuilder->BeginFigure(Vector2{});

        f.GeometrySink->SetFillModeMethod.AllowAnyCall();

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->SetFilledRegionDetermination(CanvasFilledRegionDetermination::Winding));

        ValidateStoredErrorState(E_INVALIDARG, Strings::SetFilledRegionDeterminationAfterBeginFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_SetFilledRegionDetermination_InvalidAfterFirstCompleteFigure)
    {
        SinkAccessFixture f;

        f.GeometrySink->EndFigureMethod.AllowAnyCall();

        f.PathBuilder->BeginFigure(Vector2{});
        f.PathBuilder->EndFigure(CanvasFigureLoop::Closed);

        f.GeometrySink->SetFillModeMethod.AllowAnyCall();

        Assert::AreEqual(E_INVALIDARG, f.PathBuilder->SetFilledRegionDetermination(CanvasFilledRegionDetermination::Winding));

        ValidateStoredErrorState(E_INVALIDARG, Strings::SetFilledRegionDeterminationAfterBeginFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_EndFigure)
    {
        SinkAccessFixture f;   

        f.GeometrySink->BeginFigureMethod.AllowAnyCall();

        f.GeometrySink->EndFigureMethod.SetExpectedCalls(1, 
            [](D2D1_FIGURE_END figureEnd)
            {
                Assert::AreEqual(D2D1_FIGURE_END_CLOSED, figureEnd);
        });
        ThrowIfFailed(f.PathBuilder->BeginFigure(Vector2{}));
        ThrowIfFailed(f.PathBuilder->EndFigure(CanvasFigureLoop::Closed));
    }

    TEST_METHOD_EX(CanvasPathBuilder_DoubleClose_NothingBadHappens)
    {
        SetupFixture f;

        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        Assert::AreEqual(S_OK, canvasPathBuilder->Close());
        Assert::AreEqual(S_OK, canvasPathBuilder->Close());
    }

    TEST_METHOD_EX(CanvasPathBuilder_TwoBeginFigures_ReturnsErrorAndOutputsMessage)
    {
        SetupFixture f;
        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        Assert::AreEqual(S_OK, canvasPathBuilder->BeginFigure(Vector2{}));
        Assert::AreEqual(E_INVALIDARG, canvasPathBuilder->BeginFigure(Vector2{}));
        ValidateStoredErrorState(E_INVALIDARG, Strings::TwoBeginFigures);
    }

    TEST_METHOD_EX(CanvasPathBuilder_StrayEndFigureOnNewSink_ReturnsErrorAndOutputsMessage)
    {
        SetupFixture f;
        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        Assert::AreEqual(E_INVALIDARG, canvasPathBuilder->EndFigure(CanvasFigureLoop::Closed));
        ValidateStoredErrorState(E_INVALIDARG, Strings::EndFigureWithoutBeginFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_StrayEndFigureAfterFirstFigure_ReturnsErrorAndOutputsMessage)
    {
        SetupFixture f;
        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        Assert::AreEqual(S_OK, canvasPathBuilder->BeginFigure(Vector2{}));
        Assert::AreEqual(S_OK, canvasPathBuilder->EndFigure(CanvasFigureLoop::Closed));
        Assert::AreEqual(E_INVALIDARG, canvasPathBuilder->EndFigure(CanvasFigureLoop::Closed));
        ValidateStoredErrorState(E_INVALIDARG, Strings::EndFigureWithoutBeginFigure);
    }

    TEST_METHOD_EX(CanvasPathBuilder_ClosedMidFigure_ReturnsErrorAndOutputsMessage)
    {
        SetupFixture f;
        auto canvasPathBuilder = Make<CanvasPathBuilder>(f.Device.Get());

        Assert::AreEqual(S_OK, canvasPathBuilder->BeginFigure(Vector2{}));

        ExpectHResultException(E_INVALIDARG, [&]{ f.Manager->Create(canvasPathBuilder.Get()); });
        ValidateStoredErrorState(E_INVALIDARG, Strings::PathBuilderClosedMidFigure);

    }

};