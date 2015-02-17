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

#include "MockHelpers.h"

namespace canvas
{
    using namespace ABI::Windows::ApplicationModel;


    class MockSuspendingEventArgs : public RuntimeClass<
        RuntimeClassFlags<WinRtClassicComMix>,
        ISuspendingEventArgs>
    {
    public:
        CALL_COUNTER_WITH_MOCK(get_SuspendingOperationMethod, HRESULT(ISuspendingOperation**));

        IFACEMETHODIMP get_SuspendingOperation(ISuspendingOperation** value) override
        {
            return get_SuspendingOperationMethod.WasCalled(value);
        }
    };


    class MockSuspendingOperation : public RuntimeClass<
        RuntimeClassFlags<WinRtClassicComMix>,
        ISuspendingOperation>
    {
    public:
        CALL_COUNTER_WITH_MOCK(GetDeferralMethod, HRESULT(ISuspendingDeferral**));
        CALL_COUNTER_WITH_MOCK(get_DeadlineMethod, HRESULT(DateTime*));

        IFACEMETHODIMP GetDeferral(ISuspendingDeferral** deferral) override
        {
            return GetDeferralMethod.WasCalled(deferral);
        }

        IFACEMETHODIMP get_Deadline(DateTime* value) override
        {
            return get_DeadlineMethod.WasCalled(value);
        }
    };


    class MockSuspendingDeferral : public RuntimeClass<
        RuntimeClassFlags<WinRtClassicComMix>,
        ISuspendingDeferral>
    {
    public:
        CALL_COUNTER_WITH_MOCK(CompleteMethod, HRESULT());

        IFACEMETHODIMP Complete() override
        {
            return CompleteMethod.WasCalled();
        }
    };
}
