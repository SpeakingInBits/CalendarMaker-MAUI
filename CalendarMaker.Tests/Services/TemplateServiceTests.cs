using CalendarMaker_MAUI.Services;
using FluentAssertions;

namespace CalendarMaker.Tests.Services;

/// <summary>
/// Tests for <see cref="TemplateService"/>, the registry of dev-authored templates.
/// </summary>
public class TemplateServiceTests
{
    private readonly TemplateService _service = new();

    [Fact]
    public void GetTemplateKeys_ContainsBuiltInTemplates()
    {
        var keys = _service.GetTemplateKeys().ToList();

        keys.Should().Contain("PhotoMonthlyClassic");
        keys.Should().Contain("PhotoCover");
    }

    [Fact]
    public void DefaultTemplateKey_IsRegistered()
    {
        var keys = _service.GetTemplateKeys();

        keys.Should().Contain(TemplateService.DefaultTemplateKey);
    }

    [Fact]
    public void GetTemplate_KnownKey_ReturnsMatchingDescriptor()
    {
        var template = _service.GetTemplate("PhotoMonthlyClassic");

        template.Key.Should().Be("PhotoMonthlyClassic");
        template.Name.Should().Be("Photo Monthly Classic");
        template.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetTemplate_UnknownKey_Throws()
    {
        var act = () => _service.GetTemplate("does-not-exist");

        act.Should().Throw<KeyNotFoundException>();
    }
}
