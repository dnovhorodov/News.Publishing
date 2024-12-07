using Oakton;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

[assembly: TestFramework("News.Publishing.Api.Tests.AssemblyFixture", "News.Publishing.Api.Tests")]

namespace News.Publishing.Api.Tests;

public sealed class AssemblyFixture : XunitTestFramework
{
    public AssemblyFixture(IMessageSink messageSink)
        :base(messageSink)
    {
        OaktonEnvironment.AutoStartHost = true;
    }
}
