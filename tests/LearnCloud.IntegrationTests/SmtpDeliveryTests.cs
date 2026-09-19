using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LearnCloud.Infrastructure.Email;
using Microsoft.Extensions.Options;
using Xunit;

namespace LearnCloud.IntegrationTests;

// SMTP delivery through MailKit against a real SMTP server (Mailpit, a mail catcher),
// read back through Mailpit's API.
public sealed class SmtpDeliveryTests : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.1")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(ApiPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(ApiPort).ForPath("/api/v1/info")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public async Task DisposeAsync() => await _mailpit.DisposeAsync();

    private SmtpEmailDelivery Delivery(string host, int port) => new(Options.Create(new EmailOptions
    {
        Provider = "Smtp",
        FromAddress = "noreply@learncloud.test",
        FromName = "LearnCloud",
        Smtp = { Host = host, Port = port, Security = "None" },
    }));

    [Fact]
    public async Task Email_is_delivered_over_smtp()
    {
        var delivery = Delivery(_mailpit.Hostname, _mailpit.GetMappedPublicPort(SmtpPort));
        var (html, text) = EmailLayout.Render("Term dates", new[] { "School opens on Tuesday." });

        var result = await delivery.SendAsync(new OutgoingEmail("parent@example.test", "Rudo Moyo", "Term dates over SMTP", html, text, ReplyTo: "office@school.test"), "learncloud-email-job-7", default);

        Assert.True(result.Success, result.Error);
        Assert.Equal("learncloud-email-job-7@learncloud.test", result.ProviderMessageId);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(ApiPort)}/") };
        var list = await http.GetFromJsonAsync<JsonElement>("api/v1/messages");
        var message = Assert.Single(list.GetProperty("messages").EnumerateArray());
        Assert.Equal("Term dates over SMTP", message.GetProperty("Subject").GetString());
        Assert.Equal("parent@example.test", message.GetProperty("To")[0].GetProperty("Address").GetString());
        Assert.Equal("noreply@learncloud.test", message.GetProperty("From").GetProperty("Address").GetString());
        Assert.Equal("office@school.test", message.GetProperty("ReplyTo")[0].GetProperty("Address").GetString());

        var full = await http.GetFromJsonAsync<JsonElement>($"api/v1/message/{message.GetProperty("ID").GetString()}");
        Assert.Contains("School opens on Tuesday.", full.GetProperty("HTML").GetString());
        Assert.Contains("School opens on Tuesday.", full.GetProperty("Text").GetString());
    }

    [Fact]
    public async Task An_unreachable_server_is_a_transient_failure()
    {
        // Mailpit's HTTP port does not speak SMTP; nothing listens on port 1.
        var result = await Delivery("127.0.0.1", 1).SendAsync(new OutgoingEmail("parent@example.test", null, "x", "<p>x</p>", "x"), null, default);

        Assert.False(result.Success);
        Assert.True(result.IsTransient);
    }
}
