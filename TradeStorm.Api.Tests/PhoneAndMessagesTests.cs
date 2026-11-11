using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Services;
using Xunit;

namespace TradeStorm.Api.Tests;

public class PhoneAndMessagesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PhoneAndMessagesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("380950353574", "+380950353574")]
    [InlineData("+380950353574", "+380950353574")]
    [InlineData("++380950353574", "+380950353574")]
    [InlineData("+++380 (95) 035-35-74", "+380950353574")]
    public void PhoneNumberHelper_Normalize_ShouldAlwaysProduceSingleLeadingPlus(string? input, string? expected)
    {
        var actual = PhoneNumberHelper.Normalize(input);
        Assert.Equal(expected, actual);
    }

    private static async Task<(int UserId, string Token, string Email, string Phone)> RegisterAndLoginAsync(
        HttpClient client, string firstName, string rawPhone)
    {
        var email = $"msgtest_{Guid.NewGuid():N}@loopmarket.com";
        var regRes = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            FirstName = firstName,
            LastName = "Tester",
            Email = email,
            Password = "Password123!",
            PhoneNumber = rawPhone
        });
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);

        var regJson = await regRes.Content.ReadFromJsonAsync<JsonElement>();
        int userId = regJson.GetProperty("id").GetInt32();
        string? regPhone = regJson.GetProperty("phoneNumber").GetString();
        Assert.NotNull(regPhone);
        Assert.StartsWith("+", regPhone);
        Assert.False(regPhone!.StartsWith("++"), $"Double plus returned: {regPhone}");

        var loginRes = await client.PostAsJsonAsync("/api/users/login", new LoginDto
        {
            Email = email,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginJson = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        string token = loginJson.GetProperty("token").GetString()!;

        return (userId, token, email, regPhone);
    }

    [Fact]
    public async Task RegisterAndProfile_WithDoublePlusPhone_ShouldReturnSinglePlus()
    {
        var client = _factory.CreateClient();
        var rawPhone = $"++38095{Random.Shared.Next(1000000, 9999999)}";
        var (userId, token, _, normalizedPhone) = await RegisterAndLoginAsync(client, "DoublePlus", rawPhone);

        Assert.StartsWith("+38095", normalizedPhone);
        Assert.False(normalizedPhone.StartsWith("++"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meRes = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var meJson = await meRes.Content.ReadFromJsonAsync<JsonElement>();
        var mePhone = meJson.GetProperty("phoneNumber").GetString();
        Assert.Equal(normalizedPhone, mePhone);

        var pubRes = await client.GetAsync($"/api/users/public/{userId}");
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);
        var pubJson = await pubRes.Content.ReadFromJsonAsync<JsonElement>();
        var pubPhone = pubJson.GetProperty("phoneNumber").GetString();
        Assert.Equal(normalizedPhone, pubPhone);
    }

    [Fact]
    public async Task Messages_ProgressiveHistoryAndDialogs_ShouldPaginateAndPollIncrementally()
    {
        var clientA = _factory.CreateClient();
        var userA = await RegisterAndLoginAsync(clientA, "UserA", $"38096{Random.Shared.Next(1000000, 9999999)}");
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token);

        var clientB = _factory.CreateClient();
        var userB = await RegisterAndLoginAsync(clientB, "UserB", $"++38097{Random.Shared.Next(1000000, 9999999)}");
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var clientC = _factory.CreateClient();
        var userC = await RegisterAndLoginAsync(clientC, "UserC", $"++38098{Random.Shared.Next(1000000, 9999999)}");

        // User A sends 5 messages to User B
        var sentIdsB = new List<int>();
        for (int i = 1; i <= 5; i++)
        {
            var sendRes = await clientA.PostAsJsonAsync("/api/messages/send", new
            {
                receiverId = userB.UserId,
                text = $"Message {i}"
            });
            Assert.Equal(HttpStatusCode.OK, sendRes.StatusCode);
            var msgJson = await sendRes.Content.ReadFromJsonAsync<JsonElement>();
            sentIdsB.Add(msgJson.GetProperty("id").GetInt32());
        }

        // User A sends an image data URL message to User C (newest dialog overall)
        var imgSendRes = await clientA.PostAsJsonAsync("/api/messages/send", new
        {
            receiverId = userC.UserId,
            text = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/"
        });
        Assert.Equal(HttpStatusCode.OK, imgSendRes.StatusCode);

        // 1. Progressive dialogs: offset=0&limit=1
        var dlgPage1Res = await clientA.GetAsync("/api/messages/dialogs?offset=0&limit=1");
        Assert.Equal(HttpStatusCode.OK, dlgPage1Res.StatusCode);
        Assert.Equal("2", dlgPage1Res.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("true", dlgPage1Res.Headers.GetValues("X-Has-More").Single());
        var dlgPage1 = await dlgPage1Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, dlgPage1.GetArrayLength());
        Assert.Equal(userC.UserId, dlgPage1[0].GetProperty("id").GetInt32());
        Assert.Equal("📷 Фотографія", dlgPage1[0].GetProperty("lastMessage").GetString());
        var phoneC = dlgPage1[0].GetProperty("phoneNumber").GetString();
        Assert.NotNull(phoneC);
        Assert.StartsWith("+", phoneC);
        Assert.False(phoneC!.StartsWith("++"));

        // Progressive dialogs: offset=1&limit=1
        var dlgPage2Res = await clientA.GetAsync("/api/messages/dialogs?offset=1&limit=1");
        Assert.Equal(HttpStatusCode.OK, dlgPage2Res.StatusCode);
        Assert.Equal("2", dlgPage2Res.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("false", dlgPage2Res.Headers.GetValues("X-Has-More").Single());
        var dlgPage2 = await dlgPage2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, dlgPage2.GetArrayLength());
        Assert.Equal(userB.UserId, dlgPage2[0].GetProperty("id").GetInt32());

        // 2. Progressive history: latest 2 messages (?limit=2)
        var histPage1Res = await clientA.GetAsync($"/api/messages/history/{userB.UserId}?limit=2");
        Assert.Equal(HttpStatusCode.OK, histPage1Res.StatusCode);
        Assert.Equal("5", histPage1Res.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("true", histPage1Res.Headers.GetValues("X-Has-More").Single());
        var histPage1 = await histPage1Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, histPage1.GetArrayLength());
        Assert.Equal("Message 4", histPage1[0].GetProperty("text").GetString());
        Assert.Equal("Message 5", histPage1[1].GetProperty("text").GetString());

        int firstIdPage1 = histPage1[0].GetProperty("id").GetInt32();
        int latestId = histPage1[1].GetProperty("id").GetInt32();

        // Older page (?limit=2&beforeId=...)
        var histPage2Res = await clientA.GetAsync($"/api/messages/history/{userB.UserId}?limit=2&beforeId={firstIdPage1}");
        Assert.Equal(HttpStatusCode.OK, histPage2Res.StatusCode);
        Assert.Equal("true", histPage2Res.Headers.GetValues("X-Has-More").Single());
        var histPage2 = await histPage2Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, histPage2.GetArrayLength());
        Assert.Equal("Message 2", histPage2[0].GetProperty("text").GetString());
        Assert.Equal("Message 3", histPage2[1].GetProperty("text").GetString());

        int firstIdPage2 = histPage2[0].GetProperty("id").GetInt32();

        // Oldest page (?limit=2&beforeId=...) -> only 1 message left, hasMore=false
        var histPage3Res = await clientA.GetAsync($"/api/messages/history/{userB.UserId}?limit=2&beforeId={firstIdPage2}");
        Assert.Equal(HttpStatusCode.OK, histPage3Res.StatusCode);
        Assert.Equal("false", histPage3Res.Headers.GetValues("X-Has-More").Single());
        var histPage3 = await histPage3Res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, histPage3.GetArrayLength());
        Assert.Equal("Message 1", histPage3[0].GetProperty("text").GetString());

        // 3. Incremental polling (?afterId=...)
        var emptyIncRes = await clientA.GetAsync($"/api/messages/history/{userB.UserId}?afterId={latestId}");
        Assert.Equal(HttpStatusCode.OK, emptyIncRes.StatusCode);
        var emptyInc = await emptyIncRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, emptyInc.GetArrayLength());

        var replyRes = await clientB.PostAsJsonAsync("/api/messages/send", new
        {
            receiverId = userA.UserId,
            text = "Reply from B"
        });
        Assert.Equal(HttpStatusCode.OK, replyRes.StatusCode);

        var incRes = await clientA.GetAsync($"/api/messages/history/{userB.UserId}?afterId={latestId}");
        Assert.Equal(HttpStatusCode.OK, incRes.StatusCode);
        var inc = await incRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, inc.GetArrayLength());
        Assert.Equal("Reply from B", inc[0].GetProperty("text").GetString());
    }
}
