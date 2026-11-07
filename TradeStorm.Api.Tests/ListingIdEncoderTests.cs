using TradeStorm.Api.Services;
using Xunit;

namespace TradeStorm.Api.Tests;

public class ListingIdEncoderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(25)]
    [InlineData(29)]
    [InlineData(100)]
    [InlineData(9999)]
    [InlineData(500000)]
    [InlineData(2147483647)]
    public void Encode_And_Decode_ReturnsOriginalId(int id)
    {
        // Act
        string code = ListingIdEncoder.Encode(id);
        int? decoded = ListingIdEncoder.Decode(code);

        // Assert
        Assert.NotNull(code);
        Assert.Equal(7, code.Length);
        Assert.Equal(id, decoded);
    }

    [Fact]
    public void Encode_ProducesDifferentCodesForConsecutiveIds()
    {
        string code1 = ListingIdEncoder.Encode(1);
        string code2 = ListingIdEncoder.Encode(2);
        string code3 = ListingIdEncoder.Encode(3);

        Assert.NotEqual(code1, code2);
        Assert.NotEqual(code2, code3);
        Assert.NotEqual(code1, code3);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("25", 25)]
    [InlineData("100", 100)]
    public void TryDecode_SupportsLegacyNumericIds(string input, int expectedId)
    {
        bool success = ListingIdEncoder.TryDecode(input, out int id);
        Assert.True(success);
        Assert.Equal(expectedId, id);
    }

    [Fact]
    public void TryDecode_SupportsAlphanumericCode()
    {
        string code = ListingIdEncoder.Encode(42);
        bool success = ListingIdEncoder.TryDecode(code, out int id);
        Assert.True(success);
        Assert.Equal(42, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("ABCDEFG")]
    [InlineData("-1")]
    public void TryDecode_RejectsInvalidInput(string? input)
    {
        bool success = ListingIdEncoder.TryDecode(input, out _);
        Assert.False(success);
    }
}
