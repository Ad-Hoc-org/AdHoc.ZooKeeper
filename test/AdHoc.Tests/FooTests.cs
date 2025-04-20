// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT


namespace AdHoc.Tests;
public class FooTests
{
    [Test]
    public async Task Bar_ShouldReturn0()
    {
        await Assert.That(new Foo().Bar()).IsEqualTo(0);
    }
}
