using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace TradeStorm.Api.Tests;

public class PremiumAndSecurityGatingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PremiumAndSecurityGatingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSubscriptionPlans_ShouldReturnPublicPlans()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/subscriptions/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task PublicUser_AccessingSwaggerOrDocs_ShouldReturnNotFound()
    {
        var client = _factory.CreateClient();

        var docsRes = await client.GetAsync("/docs.html");
        Assert.Equal(HttpStatusCode.NotFound, docsRes.StatusCode);

        var swaggerRes = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.NotFound, swaggerRes.StatusCode);

        var apiDocsRes = await client.GetAsync("/api-docs");
        Assert.Equal(HttpStatusCode.NotFound, apiDocsRes.StatusCode);
    }

    [Fact]
    public async Task NonPremiumUser_UploadingVideo_ShouldBeForbidden()
    {
        var client = _factory.CreateClient();

        // Register buyer user
        var email = $"buyer_{Guid.NewGuid():N}@loopmarket.com";
        var regDto = new
        {
            firstName = "Тест",
            lastName = "Покупець",
            email = email,
            password = "Password123!",
            phoneNumber = "+380501112233"
        };

        var regRes = await client.PostAsync("/api/users/register", new StringContent(
            JsonSerializer.Serialize(regDto), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);

        var loginRes = await client.PostAsync("/api/users/login", new StringContent(
            JsonSerializer.Serialize(new { email = email, password = "Password123!" }),
            Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var loginJson = await loginRes.Content.ReadAsStringAsync();
        using var loginDoc = JsonDocument.Parse(loginJson);
        var token = loginDoc.RootElement.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Check subscription status
        var statusRes = await client.GetAsync("/api/subscriptions/status");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        var statusJson = await statusRes.Content.ReadAsStringAsync();
        using var statusDoc = JsonDocument.Parse(statusJson);
        Assert.False(statusDoc.RootElement.GetProperty("isPremium").GetBoolean());

        // Attempting to upload video as non-premium
        using var form = new MultipartFormDataContent();
        var mp4Bytes = new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D };
        var fileContent = new ByteArrayContent(mp4Bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(fileContent, "file", "test_video.mp4");

        var uploadRes = await client.PostAsync("/api/listings/upload-video", form);
        Assert.Equal(HttpStatusCode.Forbidden, uploadRes.StatusCode);

        // Now checkout subscription
        var checkoutRes = await client.PostAsync("/api/subscriptions/checkout", new StringContent(
            JsonSerializer.Serialize(new { planId = "pro_monthly", paymentMethod = "card" }),
            Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, checkoutRes.StatusCode);

        // Upload video again - now user is PRO
        using var form2 = new MultipartFormDataContent();
        var fileContent2 = new ByteArrayContent(mp4Bytes);
        fileContent2.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form2.Add(fileContent2, "file", "test_video2.mp4");

        var uploadRes2 = await client.PostAsync("/api/listings/upload-video", form2);
        Assert.Equal(HttpStatusCode.OK, uploadRes2.StatusCode);
    }
}
