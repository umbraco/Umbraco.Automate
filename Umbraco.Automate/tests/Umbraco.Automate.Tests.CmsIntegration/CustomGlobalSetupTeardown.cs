using NUnit.Framework;
using Umbraco.Cms.Tests.Integration;

[SetUpFixture]
public class CustomGlobalSetupTeardown
{
    private static GlobalSetupTeardown? _setupTearDown;

    [OneTimeSetUp]
    public void SetUp()
    {
        _setupTearDown = new GlobalSetupTeardown();
        _setupTearDown.SetUp();
    }

    [OneTimeTearDown]
    public void TearDown() => _setupTearDown?.TearDown();
}
