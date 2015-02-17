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

TEST_CLASS(WinStringBuilderTests)
{
    TEST_METHOD(WinStringBuilder_Allocate)
    {
        WinStringBuilder builder;

        // Build a string.
        auto buffer = builder.Allocate(4);
        Assert::IsNotNull(buffer);

        buffer[0] = 't';
        buffer[1] = 'e';
        buffer[2] = 's';
        buffer[3] = 't';

        // Can't build other strings while this one is pending.
        ExpectHResultException(E_UNEXPECTED, [&] { builder.Allocate(4); });
        ExpectHResultException(E_UNEXPECTED, [&] { builder.Format(L"Test"); });

        // Read back the string.
        auto result = builder.Get();

        Assert::IsFalse(result.HasEmbeddedNull());
        Assert::AreEqual(0, wcscmp(L"test", (wchar_t const*)result));

        // Can't read it back twice.
        ExpectHResultException(E_UNEXPECTED, [&] { builder.Get(); });

        // After we read back the first string, can now build a new one. Let's try containing an embedded null.
        buffer = builder.Allocate(5);
        Assert::IsNotNull(buffer);

        buffer[0] = 't';
        buffer[1] = 'e';
        buffer[2] = 0;
        buffer[3] = 's';
        buffer[4] = 't';

        result = builder.Get();

        Assert::IsFalse(result.HasEmbeddedNull());
        Assert::AreEqual(0, wcscmp(L"te", static_cast<wchar_t const*>(result)));
    }


    TEST_METHOD(WinStringBuilder_Format)
    {
        WinStringBuilder builder;

        // Format a string.
        builder.Format(L"There are %d %s in the %s.", 23, L"cats", L"refrigerator");

        // Can't build other strings while this one is pending.
        ExpectHResultException(E_UNEXPECTED, [&] { builder.Allocate(4); });
        ExpectHResultException(E_UNEXPECTED, [&] { builder.Format(L"Test"); });

        // Read back the string.
        auto result = builder.Get();

        Assert::IsFalse(result.HasEmbeddedNull());
        Assert::AreEqual(0, wcscmp(L"There are 23 cats in the refrigerator.", static_cast<wchar_t const*>(result)));
    }
};
