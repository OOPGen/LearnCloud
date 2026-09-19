using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.Communication.Entities;
using LearnCloud.Communication.Services;
using LearnCloud.Infrastructure.Email;
using LearnCloud.Infrastructure.Jobs;
using LearnCloud.Messaging.Entities;
using LearnCloud.Messaging.Jobs;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.MultiTenancy.Security;
using LearnCloud.PlatformAdmin.Controllers;
using LearnCloud.PlatformBilling.Entities;
using LearnCloud.PlatformBilling.Jobs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.IntegrationTests;

// Phase 5: email delivery, the durable job queue and schedules, read-only subscriptions,
// marketing enquiries, dunning notices, and module endpoints that used to fail at runtime.
[Collection(ApiCollection.Name)]
public sealed class GoLiveHardeningTests
{
    private static int _lastYear = 2600;
    private readonly LearnCloudApiFixture _api;

    public GoLiveHardeningTests(LearnCloudApiFixture api) => _api = api;

    [Fact]
    public async Task Account_emails_are_sent_and_their_links_work()
    {
        var slug = $"mail{Guid.NewGuid():N}"[..14];
        var (_, email, password) = await _api.RegisterSchoolAsync(slug);
        await _api.RunJobsAsync();

        var verification = Assert.Single(_api.Emails.To(email), e => e.Category == "email-verification");
        var verifyLink = LinkIn(verification.HtmlBody, "/verify-email");
        Assert.StartsWith(LearnCloudApiFixture.PublicUrl, verifyLink);
        Assert.Contains(slug, verification.HtmlBody);
        var verifyQuery = Query(verifyLink);
        Assert.Equal(slug, verifyQuery["school"]);
        using var anonymous = _api.Factory.CreateClient();
        await Ok(anonymous.PostAsJsonAsync("/api/auth/verify-email", new { email = verifyQuery["email"], token = verifyQuery["token"] }));

        // Sent emails do not stay in the job table: their bodies carried the link.
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var sent = await db.Set<BackgroundJob>().Where(j => j.Kind == EmailOutbox.JobKind && j.Status == JobStatus.Succeeded).ToListAsync();
            Assert.NotEmpty(sent);
            Assert.All(sent, j => Assert.Equal(BackgroundJobRunner.RedactedPayload, j.PayloadJson));
        }

        // Forgot password for an existing account used to fail with 500.
        await Ok(anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email, tenantSlug = slug }));
        await _api.RunJobsAsync();
        var reset = Assert.Single(_api.Emails.To(email), e => e.Category == "password-reset");
        var resetQuery = Query(LinkIn(reset.HtmlBody, "/reset-password"));
        const string newPassword = "N3w!Passw0rd-Reset";
        await Ok(anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = resetQuery["email"], token = resetQuery["token"], newPassword, confirmPassword = newPassword }));

        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword, tenantSlug = slug })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password, tenantSlug = slug })).StatusCode);
        // A reset link works once.
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = resetQuery["email"], token = resetQuery["token"], newPassword, confirmPassword = newPassword })).StatusCode);
    }

    [Fact]
    public async Task Email_jobs_retry_transient_failures_and_stop_on_permanent_ones()
    {
        await _api.RunJobsAsync();
        var transientId = await QueueEmailAsync("retry@learncloud.test");
        _api.Emails.FailNext(EmailSendResult.Transient("provider timeout"));
        await _api.RunJobsAsync();

        var pending = await JobAsync(transientId);
        Assert.Equal(JobStatus.Pending, pending.Status);
        Assert.Equal(1, pending.Attempts);
        Assert.True(pending.RunAfter > DateTime.UtcNow);
        Assert.Contains("provider timeout", pending.LastError);
        Assert.NotEqual(BackgroundJobRunner.RedactedPayload, pending.PayloadJson);

        await MakeDueAsync(transientId);
        await _api.RunJobsAsync();
        var succeeded = await JobAsync(transientId);
        Assert.Equal(JobStatus.Succeeded, succeeded.Status);
        Assert.Equal(2, succeeded.Attempts);
        Assert.Equal(BackgroundJobRunner.RedactedPayload, succeeded.PayloadJson);
        Assert.Single(_api.Emails.To("retry@learncloud.test"));
        // Both attempts carry the same provider idempotency key, unique to this email (not the job id).
        var keys = _api.Emails.Attempts.Where(a => a.To == "retry@learncloud.test").Select(a => a.IdempotencyKey).ToList();
        Assert.Equal(2, keys.Count);
        Assert.Single(keys.Distinct());
        Assert.Matches("^learncloud-[0-9a-f]{32}$", keys[0]);

        var permanentId = await QueueEmailAsync("rejected@learncloud.test");
        _api.Emails.FailNext(EmailSendResult.Permanent("address rejected"));
        await _api.RunJobsAsync();
        var failed = await JobAsync(permanentId);
        Assert.Equal(JobStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Equal("address rejected", failed.LastError);
        Assert.Equal(BackgroundJobRunner.RedactedPayload, failed.PayloadJson);
        Assert.Empty(_api.Emails.To("rejected@learncloud.test"));
    }

    [Fact]
    public async Task Only_one_worker_claims_a_job_and_an_expired_lease_is_taken_over()
    {
        await _api.RunJobsAsync();
        long jobId;
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>().Enqueue("test.unknown-kind", new { }, tenantId: null, maxAttempts: 3);
            await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().SaveChangesAsync();
            jobId = job.Id;
        }

        var first = ActivatorUtilities.CreateInstance<BackgroundJobRunner>(_api.Factory.Services);
        var second = ActivatorUtilities.CreateInstance<BackgroundJobRunner>(_api.Factory.Services);
        var claims = await Task.WhenAll(first.TryClaimAsync(default), second.TryClaimAsync(default));
        var winner = Assert.Single(claims, c => c is not null)!;
        Assert.Equal(jobId, winner.Id);
        var winnerRunner = claims[0] is not null ? first : second;
        var otherRunner = claims[0] is not null ? second : first;
        Assert.Null(await otherRunner.TryClaimAsync(default));

        // The winner stalls: once its lease runs out, the other instance takes the job over.
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<BackgroundJob>()
                .Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedUntil, DateTime.UtcNow.AddMinutes(-1)));
        var takeover = await otherRunner.TryClaimAsync(default);
        Assert.Equal(jobId, takeover?.Id);
        Assert.Equal(2, takeover!.Attempts);

        // The stalled instance finishing late changes nothing; the new owner records the result.
        await winnerRunner.RunAsync(winner, default);
        Assert.Equal(JobStatus.Running, (await JobAsync(jobId)).Status);
        await otherRunner.RunAsync(takeover, default);
        var finished = await JobAsync(jobId);
        Assert.Equal(JobStatus.Failed, finished.Status);
        Assert.Contains("No handler", finished.LastError);
    }

    [Fact]
    public async Task Scheduled_jobs_run_once_when_due_and_keep_their_next_run()
    {
        var runner = _api.Factory.Services.GetRequiredService<ScheduledJobRunner>();
        Assert.Contains("communication-rules", runner.JobNames);
        Assert.Contains("billing-dunning", runner.JobNames);
        Assert.Contains("background-job-cleanup", runner.JobNames);

        await runner.RunDueAsync(default); // creates the schedule rows; nothing is due yet
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<ScheduledJobState>()
                .Where(s => s.Name == "background-job-cleanup").ExecuteUpdateAsync(s => s.SetProperty(x => x.NextRunAt, DateTime.UtcNow.AddMinutes(-1)));

        Assert.Equal(new[] { "background-job-cleanup" }, await runner.RunDueAsync(default));
        Assert.Empty(await runner.RunDueAsync(default));

        await using var check = _api.Factory.Services.CreateAsyncScope();
        var state = await check.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<ScheduledJobState>().SingleAsync(s => s.Name == "background-job-cleanup");
        Assert.Equal("ok", state.LastResult);
        Assert.Null(state.LockedBy);
        Assert.InRange(state.NextRunAt, DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
    }

    [Fact]
    public async Task Message_batches_are_sent_by_the_job_worker_and_sms_fails_honestly()
    {
        await _api.RunJobsAsync();
        var recipient = $"parent{Guid.NewGuid():N}"[..14] + "@learncloud.test";
        var emailBatch = await QueueBatchAsync(MessageChannel.Email, recipient);
        var smsBatch = await QueueBatchAsync(MessageChannel.Sms, "+263771234567");
        await _api.RunJobsAsync();

        await using var scope = _api.Factory.Services.CreateAsyncScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolA.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();

        Assert.Equal(MessageStatus.Sent, (await db.Set<MessageBatch>().SingleAsync(b => b.Id == emailBatch)).Status);
        var emailLog = await db.Set<MessageDeliveryLog>().SingleAsync(l => l.BatchId == emailBatch);
        Assert.Equal(MessageStatus.Sent, emailLog.Status);
        Assert.Equal("Capture", emailLog.Provider);
        var sent = Assert.Single(_api.Emails.To(recipient));
        Assert.Equal("Term dates", sent.Subject);

        Assert.Equal(MessageStatus.Failed, (await db.Set<MessageBatch>().SingleAsync(b => b.Id == smsBatch)).Status);
        var smsLog = await db.Set<MessageDeliveryLog>().SingleAsync(l => l.BatchId == smsBatch);
        Assert.Equal(MessageStatus.Failed, smsLog.Status);
        Assert.Contains("no SMS provider", smsLog.FailureReason);
        Assert.Equal(0, smsLog.RetryCount);
    }

    [Fact]
    public async Task A_resumed_message_batch_counts_messages_sent_by_the_earlier_attempt()
    {
        await _api.RunJobsAsync();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var (first, second) = ($"first{tag}@learncloud.test", $"second{tag}@learncloud.test");
        var batchId = await QueueBatchAsync(MessageChannel.Email, first, second);

        // The earlier attempt sent the first message, then the instance stopped.
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolA.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var log = await db.Set<MessageDeliveryLog>().SingleAsync(l => l.BatchId == batchId && l.RecipientAddress == first);
            log.Status = MessageStatus.Sent;
            (await db.Set<MessageBatch>().SingleAsync(b => b.Id == batchId)).Status = MessageStatus.Sending;
            await db.SaveChangesAsync();
        }
        await _api.RunJobsAsync();

        await using var check = _api.Factory.Services.CreateAsyncScope();
        using var _ = check.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolA.TenantId);
        var batch = await check.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<MessageBatch>().SingleAsync(b => b.Id == batchId);
        Assert.Equal(MessageStatus.Sent, batch.Status);
        Assert.Equal(2, batch.SentCount);
        Assert.Equal(0, batch.FailedCount);
        Assert.Equal(100, batch.ProgressPercent);
        Assert.Empty(_api.Emails.To(first));
        Assert.Single(_api.Emails.To(second));
    }

    [Fact]
    public async Task A_suspended_school_can_read_but_not_change_records()
    {
        using var b = _api.ClientFor(_api.SchoolB);
        var original = await SetSubscriptionAsync(_api.SchoolB.TenantId, SubscriptionState.Suspended);
        try
        {
            var blocked = await b.PostAsJsonAsync("/api/academic/subjects", new { Name = "Blocked", Code = $"RO{Random.Shared.Next(1000, 9999)}", IsCore = true });
            Assert.Equal(HttpStatusCode.PaymentRequired, blocked.StatusCode);
            Assert.Equal("account_read_only", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

            Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/academic/subjects")).StatusCode);
            // Exports that happen to be POST requests stay available.
            var export = await b.PostAsJsonAsync("/api/hr/payroll-export", new { Year = 2026, Month = 9, StaffIds = (long[]?)null, Format = "generic_csv" });
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            var status = await Ok(b.GetAsync("/api/billing/read-only-status"));
            Assert.True(status.GetProperty("isReadOnly").GetBoolean());
            Assert.Contains("billing@learncloud.co.zw", status.GetProperty("banner").GetString());
            // Used to fail with 500 for every school.
            var subscription = await Ok(b.GetAsync("/api/billing/subscription"));
            Assert.False(subscription.GetProperty("canEdit").GetBoolean());

            // Past due is a grace period: changes are still allowed.
            await SetSubscriptionAsync(_api.SchoolB.TenantId, SubscriptionState.PastDue);
            var allowed = await b.PostAsJsonAsync("/api/academic/subjects", new { Name = "Grace", Code = $"PD{Random.Shared.Next(1000, 9999)}", IsCore = true });
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        }
        finally
        {
            await SetSubscriptionAsync(_api.SchoolB.TenantId, original);
        }

        // Anyone could read any school's billing state before.
        using var anonymous = _api.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/billing/read-only-status?tenantId={_api.SchoolB.TenantId}")).StatusCode);
    }

    [Fact]
    public async Task Marketing_enquiries_are_stored_and_emailed_to_sales()
    {
        using var anonymous = _api.Factory.CreateClient();
        var school = $"Enquiry School {Guid.NewGuid():N}"[..28];
        var demo = await anonymous.PostAsJsonAsync("/api/public/enquiries", new
        {
            schoolName = school, contactName = "Rudo Moyo", role = "Bursar", email = "Rudo@Example.test", phone = "+263 77 123 4567",
            learnerCount = "301-800", currentSystem = "Excel", consent = true, source = "marketing_book_demo", hq = "Bulawayo", timestamp = DateTime.UtcNow,
        });
        Assert.Equal(HttpStatusCode.Accepted, demo.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await anonymous.PostAsJsonAsync("/api/public/enquiries", new
        {
            schoolName = school, contactName = "No Consent", email = "nc@example.test", consent = false, source = "marketing_book_demo",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await anonymous.PostAsJsonAsync("/api/public/enquiries", new
        {
            schoolName = school, contactName = "Tawanda", email = "t@example.test", message = "Fees at term end", source = "marketing_contact",
        })).StatusCode);

        await _api.RunJobsAsync();
        var toSales = _api.Emails.To(LearnCloudApiFixture.SalesInbox).Where(e => e.Subject.Contains(school)).ToList();
        Assert.Equal(2, toSales.Count);
        Assert.Contains(toSales, e => e.Subject.StartsWith("Demo request") && e.ReplyTo == "rudo@example.test");
        Assert.Contains(toSales, e => e.Subject.StartsWith("Contact message") && e.HtmlBody.Contains("Fees at term end"));

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<SalesEnquiry>().CountAsync(e => e.SchoolName == school));

        using var a = _api.ClientFor(_api.SchoolA);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/platform/enquiries")).StatusCode);
    }

    [Fact]
    public async Task Trial_reminders_are_emailed_to_the_school()
    {
        await _api.RunJobsAsync();
        string contact;
        (DateTime? Started, DateTime? Ends) original;
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            contact = (await db.Tenants.SingleAsync(t => t.Id == _api.SchoolB.TenantId)).ContactEmail!;
            var sub = await db.Set<Subscription>().SingleAsync(s => s.TenantId == _api.SchoolB.TenantId);
            original = (sub.TrialStartedAt, sub.TrialEndsAt);
            sub.TrialStartedAt = DateTime.UtcNow.AddDays(-7).AddHours(-1);
            sub.TrialEndsAt = DateTime.UtcNow.AddDays(7);
            await db.SaveChangesAsync();
        }

        try
        {
            await using (var scope = _api.Factory.Services.CreateAsyncScope())
            using (scope.ServiceProvider.GetRequiredService<INoTenantOperation>().BeginScope("Integration test dunning run", 0, PrivilegedRoles.SystemJob))
                await scope.ServiceProvider.GetRequiredService<DunningJob>().RunAsync();
            await _api.RunJobsAsync();

            // A second run the same day (e.g. after a restart) does not notify again.
            await using (var scope = _api.Factory.Services.CreateAsyncScope())
            using (scope.ServiceProvider.GetRequiredService<INoTenantOperation>().BeginScope("Integration test dunning rerun", 0, PrivilegedRoles.SystemJob))
                await scope.ServiceProvider.GetRequiredService<DunningJob>().RunAsync();
            await _api.RunJobsAsync();

            var reminder = Assert.Single(_api.Emails.To(contact), e => e.Category == "billing");
            Assert.Contains("trial ends soon", reminder.Subject);
            await using var check = _api.Factory.Services.CreateAsyncScope();
            var events = await check.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<DunningEvent>()
                .Where(e => e.TenantId == _api.SchoolB.TenantId && e.EventType == "trial_reminder_day_7").ToListAsync();
            Assert.True(Assert.Single(events).IsSuccess);
        }
        finally
        {
            await using var scope = _api.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var sub = await db.Set<Subscription>().SingleAsync(s => s.TenantId == _api.SchoolB.TenantId);
            (sub.TrialStartedAt, sub.TrialEndsAt) = original;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Hostel_occupancy_report_and_roll_calls_work()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var code = $"H{Random.Shared.Next(1000, 9999)}";
        var blockId = Id(await Ok(a.PostAsJsonAsync("/api/hostel/blocks", new { name = $"House {code}", code, genderDesignation = "mixed", capacity = 2, totalRooms = 1 })));
        var roomId = Id(await Ok(a.PostAsJsonAsync("/api/hostel/rooms", new { blockId, roomNumber = "101", floor = 1, capacity = 2, genderDesignation = "mixed" })));
        // The room created beds 101A and 101B; add a third, and refuse a duplicate number.
        await Ok(a.PostAsJsonAsync("/api/hostel/beds", new { roomId, bedNumber = "101C", condition = "good" }));
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync("/api/hostel/beds", new { roomId, bedNumber = "101A", condition = "good" })).StatusCode);

        var report = await Ok(a.GetAsync($"/api/hostel/reports/occupancy?blockId={blockId}"));
        Assert.Equal(3, report.GetProperty("capacity").GetInt32());
        Assert.Equal(3, report.GetProperty("available").GetInt32());
        Assert.Equal(0, report.GetProperty("occupied").GetInt32());
        Assert.Equal("101", Assert.Single(report.GetProperty("rooms").EnumerateArray()).GetProperty("roomNumber").GetString());
        Assert.Equal("All blocks", (await Ok(a.GetAsync("/api/hostel/reports/occupancy"))).GetProperty("blockName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync("/api/hostel/reports/occupancy?blockId=987654321")).StatusCode);

        var rollCall = await Ok(a.PostAsJsonAsync("/api/hostel/roll-calls", new { blockId, rollCallDate = DateTime.UtcNow.Date, rollCallType = "nightly" }));
        Assert.Equal(0, rollCall.GetProperty("totalExpected").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync("/api/hostel/roll-calls", new { blockId, rollCallDate = DateTime.UtcNow.Date, rollCallType = "nightly" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/hostel/roll-calls/{Id(rollCall)}/mark", new { entries = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Absence_rules_notify_guardians_once_per_current_absence_streak()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var y = Interlocked.Increment(ref _lastYear);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var yearId = Id(await Ok(a.PostAsJsonAsync("/api/academic/years", new { name = $"R{y}", startDate = $"{y}-01-10", endDate = $"{y}-12-05", isCurrent = false })));
        var termId = Id(await Ok(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", new { name = "Term 1", termNumber = 1, startDate = $"{y}-01-10", endDate = $"{y}-04-10", isCurrent = false })));
        var gradeId = Id(await Ok(a.PostAsJsonAsync("/api/academic/grades", new { academicYearId = yearId, name = "Form 4", code = "F4", levelOrder = 4 })));
        var streamId = Id(await Ok(a.PostAsJsonAsync($"/api/academic/grades/{gradeId}/streams", new { name = "Gold", capacity = 30 })));
        async Task<(long StudentId, string GuardianEmail)> Enrol(string name, int phoneSuffix)
        {
            var email = $"{name}{tag}@learncloud.test";
            var student = await Ok(a.PostAsJsonAsync("/api/students", new
            {
                firstName = name, lastName = "Absent", streamId, termId, enrolmentDate = $"{y}-01-12",
                guardian = new
                {
                    newGuardian = new { firstName = "Parent", lastName = name, phone = $"+2637{Random.Shared.Next(1000000, 9999999)}{phoneSuffix}", email },
                    relationshipType = "mother", isPrimaryContact = true, isBillingContact = false, isEmergencyContact = true, canPickup = true,
                },
            }));
            return (Id(student), email);
        }
        var (current, currentParent) = await Enrol("current", 1);
        var (old, oldParent) = await Enrol("old", 2);

        // Saved segments with a filter failed to preview, like rules with settings.
        var segment = await Ok(a.PostAsJsonAsync("/api/communication/segments", new { name = $"Gold {tag}", description = "", audienceType = "stream", filterJson = $"{{\"gradeId\":{gradeId},\"streamId\":{streamId}}}", isDynamic = true }));
        var preview = await Ok(a.PostAsJsonAsync("/api/communication/segments/preview", new { segmentId = Id(segment), audienceType = "stream", filterJson = "{}" }));
        Assert.Equal(2, preview.GetProperty("totalGuardians").GetInt32());

        long ruleId;
        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolA.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            var today = DateTime.UtcNow.Date;
            foreach (var (studentId, dates) in new[] { (current, new[] { today, today.AddDays(-1) }), (old, new[] { today.AddDays(-40), today.AddDays(-41) }) })
                foreach (var date in dates)
                {
                    var register = new AttendanceRegister { TenantId = _api.SchoolA.TenantId, AcademicYearId = yearId, TermId = termId, GradeId = gradeId, StreamId = streamId, AttendanceDate = date, Status = "submitted" };
                    register.Records.Add(new AttendanceRecord { TenantId = _api.SchoolA.TenantId, StudentId = studentId, GradeId = gradeId, StreamId = streamId, AcademicYearId = yearId, TermId = termId, AttendanceDate = date, Status = AttendanceStatus.Absent });
                    db.Set<AttendanceRegister>().Add(register);
                }
            // "dynamic" is the default audience of a rule; it used to make every rule fail.
            var rule = new CommunicationRule { TenantId = _api.SchoolA.TenantId, Name = "Absent two days", Code = $"abs{tag}", EventType = "absence_n_days", ConfigJson = "{\"n\":2}", Channel = "email", AudienceType = "dynamic" };
            db.Set<CommunicationRule>().Add(rule);
            await db.SaveChangesAsync();
            ruleId = rule.Id;
        }

        try
        {
            // Two hourly runs of the rules.
            for (var run = 0; run < 2; run++)
            {
                await using var scope = _api.Factory.Services.CreateAsyncScope();
                using (scope.ServiceProvider.GetRequiredService<INoTenantOperation>().BeginScope("Integration test rules run", 0, PrivilegedRoles.SystemJob))
                    await scope.ServiceProvider.GetRequiredService<ICommunicationRuleEngine>().ProcessScheduledRulesAsync();
                await _api.RunJobsAsync();
            }

            Assert.Single(_api.Emails.To(currentParent));
            Assert.Empty(_api.Emails.To(oldParent));
        }
        finally
        {
            await using var scope = _api.Factory.Services.CreateAsyncScope();
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolA.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            (await db.Set<CommunicationRule>().SingleAsync(r => r.Id == ruleId)).IsActive = false;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Attendance_register_lists_current_students_who_joined_in_an_earlier_term()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var y = Interlocked.Increment(ref _lastYear);
        var yearId = Id(await Ok(a.PostAsJsonAsync("/api/academic/years", new { name = $"A{y}", startDate = $"{y}-01-10", endDate = $"{y}-12-05", isCurrent = false })));
        var term1 = Id(await Ok(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", new { name = "Term 1", termNumber = 1, startDate = $"{y}-01-10", endDate = $"{y}-04-10", isCurrent = false })));
        var term2 = Id(await Ok(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", new { name = "Term 2", termNumber = 2, startDate = $"{y}-05-05", endDate = $"{y}-08-05", isCurrent = false })));
        var gradeId = Id(await Ok(a.PostAsJsonAsync("/api/academic/grades", new { academicYearId = yearId, name = "Form 3", code = "F3", levelOrder = 3 })));
        var streamId = Id(await Ok(a.PostAsJsonAsync($"/api/academic/grades/{gradeId}/streams", new { name = "Red", capacity = 30 })));
        var stays = Id(await Ok(a.PostAsJsonAsync("/api/students", new { firstName = "Stays", lastName = "Enrolled", streamId, termId = term1, enrolmentDate = $"{y}-01-12" })));
        var leaves = Id(await Ok(a.PostAsJsonAsync("/api/students", new { firstName = "Has", lastName = "Left", streamId, termId = term1, enrolmentDate = $"{y}-01-12" })));
        await Ok(a.PostAsJsonAsync($"/api/students/{leaves}/exit", new { reason = "withdrawn", exitDate = $"{y}-03-01" }));

        var register = await Ok(a.GetAsync($"/api/attendance/register?gradeId={gradeId}&streamId={streamId}&attendanceDate={y}-05-12&periodNumber=1&academicYearId={yearId}&termId={term2}"));
        var listed = register.GetProperty("students").EnumerateArray().Select(s => s.GetProperty("studentId").GetInt64()).ToList();
        Assert.Contains(stays, listed);
        Assert.DoesNotContain(leaves, listed);
    }

    // ---- helpers ------------------------------------------------------------------------

    private async Task<long> QueueEmailAsync(string to)
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var (html, text) = EmailLayout.Render("Test", new[] { "Hello" });
        scope.ServiceProvider.GetRequiredService<IEmailOutbox>().Queue(new OutgoingEmail(to, null, "Test", html, text, Category: "test"), tenantId: null);
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        await db.SaveChangesAsync();
        return db.ChangeTracker.Entries<BackgroundJob>().Single().Entity.Id;
    }

    private async Task<BackgroundJob> JobAsync(long id)
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<BackgroundJob>().AsNoTracking().SingleAsync(j => j.Id == id);
    }

    private async Task MakeDueAsync(long id)
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>().Set<BackgroundJob>()
            .Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.RunAfter, DateTime.UtcNow.AddSeconds(-1)));
    }

    private async Task<long> QueueBatchAsync(MessageChannel channel, params string[] addresses)
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var tenantId = _api.SchoolA.TenantId;
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var batch = new MessageBatch
        {
            TenantId = tenantId, BatchNumber = $"TEST-{Guid.NewGuid():N}"[..20], Title = "Test batch", Channel = channel,
            AudienceType = AudienceType.Manual, Body = "School opens on Tuesday.", Subject = "Term dates",
            TotalRecipients = addresses.Length, Status = MessageStatus.Queued, QueuedAt = DateTime.UtcNow,
        };
        db.Set<MessageBatch>().Add(batch);
        await db.SaveChangesAsync();
        foreach (var address in addresses)
            db.Set<MessageDeliveryLog>().Add(new MessageDeliveryLog
            {
                TenantId = tenantId, BatchId = batch.Id, RecipientName = "Parent", RecipientAddress = address, Channel = channel,
                Status = MessageStatus.Queued, RenderedBody = batch.Body, RenderedSubject = batch.Subject,
            });
        scope.ServiceProvider.GetRequiredService<IMessageBatchQueue>().Enqueue(tenantId, batch.Id);
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<SubscriptionState> SetSubscriptionAsync(long tenantId, SubscriptionState state)
    {
        await using var scope = _api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
        var sub = await db.Set<Subscription>().SingleAsync(s => s.TenantId == tenantId);
        var previous = sub.State;
        sub.State = state;
        if (state == SubscriptionState.Suspended) sub.SuspendedSince = DateTime.UtcNow;
        if (state == SubscriptionState.PastDue) sub.PastDueSince = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return previous;
    }

    private static string LinkIn(string html, string path)
    {
        var match = Regex.Matches(html, "href=\"([^\"]+)\"").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).FirstOrDefault(u => u.Contains(path));
        return match ?? throw new InvalidOperationException($"No {path} link in the email");
    }

    private static Dictionary<string, string> Query(string url) =>
        new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));

    private static long Id(JsonElement e) => e.GetProperty("id").GetInt64();

    private static async Task<JsonElement> Ok(Task<HttpResponseMessage> call)
    {
        var response = await call;
        await LearnCloudApiFixture.EnsureSuccessAsync(response, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}");
        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
