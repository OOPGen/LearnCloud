using System.Net;
using System.Text.Json;
using LearnCloud.Infrastructure.Email;
using LearnCloud.Infrastructure.Jobs;
using LearnCloud.PlatformAdmin.Controllers;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace LearnCloud.Infrastructure.Tests;

public class EmailOptionsTests
{
    [Fact]
    public void Log_provider_needs_no_settings() => Assert.Empty(new EmailOptions().Problems());

    [Fact]
    public void Resend_needs_a_sender_and_an_api_key()
    {
        var problems = new EmailOptions { Provider = "Resend" }.Problems().ToList();
        Assert.Contains(problems, p => p.Contains("FromAddress"));
        Assert.Contains(problems, p => p.Contains("ApiKey"));
        Assert.Empty(new EmailOptions { Provider = "resend", FromAddress = "noreply@learncloud.co.zw", Resend = { ApiKey = "re_123" } }.Problems());
    }

    [Fact]
    public void Smtp_needs_a_host_and_a_known_security_mode()
    {
        var options = new EmailOptions { Provider = "Smtp", FromAddress = "noreply@learncloud.co.zw", Smtp = { Host = "", Security = "Sometimes" } };
        var problems = options.Problems().ToList();
        Assert.Contains(problems, p => p.Contains("Host"));
        Assert.Contains(problems, p => p.Contains("Security"));
    }

    [Theory]
    [InlineData("7", false)]
    [InlineData("1", false)]
    [InlineData("none", true)]
    [InlineData("SslOnConnect", true)]
    public void Smtp_security_must_be_a_named_mode(string security, bool valid)
    {
        var options = new EmailOptions { Provider = "Smtp", FromAddress = "noreply@learncloud.co.zw", Smtp = { Host = "smtp.example.test", Security = security } };
        Assert.Equal(valid, !options.Problems().Any());
    }

    [Fact]
    public void Unknown_provider_is_rejected() =>
        Assert.Single(new EmailOptions { Provider = "Carrier pigeon" }.Problems());
}

public class ResendEmailDeliveryTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? Request;
        public string? Body;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return _respond(request);
        }
    }

    private static (ResendEmailDelivery Delivery, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.test/") };
        var options = Options.Create(new EmailOptions { Provider = "Resend", FromAddress = "noreply@learncloud.co.zw", FromName = "LearnCloud", Resend = { ApiKey = "re_secret" } });
        return (new ResendEmailDelivery(http, options), handler);
    }

    private static readonly OutgoingEmail Email = new("parent@example.test", "Parent", "Term dates", "<p>Hi</p>", "Hi", ReplyTo: "office@school.test", Category: "school message");

    [Fact]
    public async Task Sends_the_documented_request_and_returns_the_id()
    {
        var (delivery, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"email_123\"}") });

        var result = await delivery.SendAsync(Email, "job-42", default);

        Assert.True(result.Success);
        Assert.Equal("email_123", result.ProviderMessageId);
        Assert.Equal("https://api.resend.test/emails", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer re_secret", handler.Request.Headers.Authorization!.ToString());
        Assert.Equal("job-42", Assert.Single(handler.Request.Headers.GetValues("Idempotency-Key")));
        var body = JsonDocument.Parse(handler.Body!).RootElement;
        Assert.Equal("\"LearnCloud\" <noreply@learncloud.co.zw>", body.GetProperty("from").GetString());
        Assert.Equal("parent@example.test", body.GetProperty("to")[0].GetString());
        Assert.Equal("office@school.test", body.GetProperty("reply_to").GetString());
        Assert.Equal("school_message", body.GetProperty("tags")[0].GetProperty("value").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task Failures_are_classified_for_retrying(HttpStatusCode status, bool transient)
    {
        var (delivery, _) = Create(_ => new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"nope\"}") });
        var result = await delivery.SendAsync(Email, null, default);
        Assert.False(result.Success);
        Assert.Equal(transient, result.IsTransient);
        Assert.Contains(((int)status).ToString(), result.Error);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("office@")]
    [InlineData("@school.test")]
    [InlineData("a@b@c")]
    public async Task An_unusable_reply_to_address_is_left_out(string replyTo)
    {
        var (delivery, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"email_1\"}") });
        var result = await delivery.SendAsync(Email with { ReplyTo = replyTo }, "k", default);
        Assert.True(result.Success);
        Assert.DoesNotContain("reply_to", handler.Body);
    }

    [Fact]
    public async Task Network_errors_are_transient()
    {
        var (delivery, _) = Create(_ => throw new HttpRequestException("connection refused"));
        var result = await delivery.SendAsync(Email, null, default);
        Assert.True(result.IsTransient);
    }
}

public class EmailLayoutAndLinkTests
{
    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://127.0.0.1:8788", true)]
    [InlineData("https://school.learncloud.localhost", true)]
    [InlineData("", true)]
    [InlineData("https://app.learncloud.co.zw", false)]
    public void Local_public_urls_are_detected(string url, bool local) =>
        Assert.Equal(local, EmailConfigurationCheck.LooksLocal(url));

    [Fact]
    public void Layout_encodes_text_and_links()
    {
        var (html, text) = EmailLayout.Render("Hello <b>", new[] { "Tom & Jerry" }, "Open", "https://app.test/reset?a=1&b=<x>");
        Assert.Contains("Hello &lt;b&gt;", html);
        Assert.Contains("Tom &amp; Jerry", html);
        Assert.Contains("href=\"https://app.test/reset?a=1&amp;b=&lt;x&gt;\"", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("Open: https://app.test/reset?a=1&b=<x>", text);
    }

    [Fact]
    public void App_links_escape_query_values_and_skip_empty_ones()
    {
        var app = new AppOptions { PublicUrl = "https://app.learncloud.co.zw/" };
        var link = app.Link("/reset-password", new Dictionary<string, string?> { ["email"] = "a+b@x.test", ["school"] = null, ["token"] = "t/o=k" });
        Assert.Equal("https://app.learncloud.co.zw/reset-password?email=a%2Bb%40x.test&token=t%2Fo%3Dk", link);
    }

    [Theory]
    [InlineData("rudo@example.test", "r***@example.test")]
    [InlineData("a@b.c", "***")]
    public void Log_provider_masks_addresses(string address, string masked) =>
        Assert.Equal(masked, LogEmailDelivery.MaskAddress(address));
}

public class SalesEnquiryValidatorTests
{
    private static SalesEnquiryRequest Request(string schoolName = "Petra High", bool consent = true, string? source = "marketing_book_demo") =>
        new(schoolName, "Tendai Moyo", "tendai@petra.test", "+263 77 123 4567", "BURSAR", "301-800", null, "Fees take days.\nEvery term.", consent, source);

    [Fact]
    public void A_complete_demo_request_is_valid() =>
        Assert.True(new SalesEnquiryValidator().Validate(Request()).IsValid);

    [Theory]
    [InlineData("Petra High\r\nBcc: someone@example.test")]
    [InlineData("Petra\tHigh")]
    public void Single_line_fields_refuse_control_characters(string schoolName) =>
        Assert.False(new SalesEnquiryValidator().Validate(Request(schoolName)).IsValid);

    [Theory]
    [InlineData("marketing_book_demo")]
    [InlineData(null)]
    public void Demo_requests_need_consent(string? source) =>
        Assert.False(new SalesEnquiryValidator().Validate(Request(consent: false, source: source)).IsValid);

    [Fact]
    public void Contact_messages_do_not_need_consent() =>
        Assert.True(new SalesEnquiryValidator().Validate(Request(consent: false, source: "marketing_contact")).IsValid);
}

public class JobBackoffTests
{
    [Theory]
    [InlineData(1, 24, 36)]
    [InlineData(2, 48, 72)]
    [InlineData(20, 2880, 4320)]
    public void Backoff_doubles_and_is_capped_at_an_hour(int attempt, double minSeconds, double maxSeconds)
    {
        for (var i = 0; i < 20; i++)
            Assert.InRange(BackgroundJobRunner.Backoff(attempt).TotalSeconds, minSeconds, maxSeconds);
    }
}

public class SubscriptionAccessTests
{
    [Theory]
    [InlineData(SubscriptionState.Trialing, false)]
    [InlineData(SubscriptionState.Active, false)]
    [InlineData(SubscriptionState.PastDue, false)]
    [InlineData(SubscriptionState.Suspended, true)]
    [InlineData(SubscriptionState.Expired, true)]
    [InlineData(SubscriptionState.Cancelled, true)]
    [InlineData(SubscriptionState.Archived, true)]
    public void Only_suspended_expired_cancelled_and_archived_are_read_only(SubscriptionState state, bool readOnly) =>
        Assert.Equal(readOnly, SubscriptionAccess.IsReadOnly(state));
}
