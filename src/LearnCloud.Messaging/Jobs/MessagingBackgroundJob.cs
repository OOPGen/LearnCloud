using LearnCloud.Messaging.Entities;
using LearnCloud.Messaging.Services.Providers;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Messaging.Jobs;

// Sending runs as background job with batching, retry with backoff, and per-tenant rate limits

public class MessagingBackgroundJob
{
    private readonly LearnCloudDbContext _db;
    private readonly IMessagingProviderFactory _providerFactory;
    private readonly ILogger<MessagingBackgroundJob> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly INoTenantOperation _noTenantOp;

    public MessagingBackgroundJob(LearnCloudDbContext db, IMessagingProviderFactory providerFactory, ILogger<MessagingBackgroundJob> logger, ITenantContext tenantContext, INoTenantOperation noTenantOp)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
        _tenantContext = tenantContext;
        _noTenantOp = noTenantOp;
    }

    // Entry point for Hangfire/Quartz: ProcessBatchAsync(batchId)
    // SECURITY FIX H3: Background jobs must set tenant context via BeginTenantScope
    // Uses explicit no-tenant to get batch's tenantId, then per-tenant scope
    public async Task ProcessBatchAsync(long batchId, CancellationToken ct = default)
    {
        // SECURITY: Use explicit no-tenant scope to fetch batch's tenantId (platform operation)
        MessageBatch? batchUnfiltered = null;
        await _noTenantOp.ExecuteAsync($"Background job fetching batch {batchId} for tenant isolation", async () =>
        {
            batchUnfiltered = await _db.Set<MessageBatch>().FirstOrDefaultAsync(b => b.Id == batchId && !b.IsDeleted, ct);
        }, ct);

        if (batchUnfiltered == null) throw new InvalidOperationException($"Batch {batchId} not found");
        
        // SECURITY: Set tenant scope from batch's tenantId for all subsequent queries
        using var tenantScope = _tenantContext.BeginTenantScope(batchUnfiltered.TenantId);
        
        var batch = await _db.Set<MessageBatch>().FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == batchUnfiltered.TenantId && !b.IsDeleted, ct);
        if (batch == null) throw new InvalidOperationException($"Batch {batchId} not found for tenant {batchUnfiltered.TenantId}");

        if (batch.Status != MessageStatus.Queued && batch.Status != MessageStatus.Sending)
        {
            _logger.LogWarning("Batch {BatchId} not in Queued/Sending status, current {Status}", batchId, batch.Status);
            return;
        }

        batch.Status = MessageStatus.Sending;
        batch.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var logs = await _db.Set<MessageDeliveryLog>().Where(l => l.BatchId == batchId && l.TenantId == batch.TenantId && !l.IsDeleted && l.Status == MessageStatus.Queued).OrderBy(l => l.Id).ToListAsync(ct);

        var logs = await _db.Set<MessageDeliveryLog>().Where(l => l.BatchId == batchId && l.TenantId == batch.TenantId && !l.IsDeleted && l.Status == MessageStatus.Queued).OrderBy(l => l.Id).ToListAsync(ct);

        // Batching: e.g., 50 messages per batch iteration
        const int batchSize = 50;
        var providerSettings = await _db.Set<MessagingProviderSettings>().FirstOrDefaultAsync(s => s.TenantId == batch.TenantId && s.Channel == batch.Channel && s.IsActive && !s.IsDeleted, ct);
        var rateLimitPerSecond = providerSettings?.RateLimitPerSecond ?? 10;
        var delayBetweenBatches = TimeSpan.FromSeconds(1.0 / rateLimitPerSecond * batchSize);

        int sent = 0, delivered = 0, failed = 0;
        decimal actualCost = 0m;

        foreach (var chunk in logs.Chunk(batchSize))
        {
            foreach (var log in chunk)
            {
                if (ct.IsCancellationRequested) break;

                // Guardian contact preferences and opt-out that is always honoured - double check before send (in case opt-out happened after queuing)
                var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == batch.TenantId && p.GuardianId == log.GuardianId && !p.IsDeleted, ct);
                if (pref != null)
                {
                    if (batch.Channel == MessageChannel.Sms && (pref.SmsOptOut || !pref.SmsOptIn))
                    {
                        log.Status = MessageStatus.Cancelled;
                        log.FailureReason = "Opted out SMS";
                        log.IsOptedOut = true;
                        failed++;
                        continue;
                    }
                    if (batch.Channel == MessageChannel.Email && (pref.EmailOptOut || !pref.EmailOptIn))
                    {
                        log.Status = MessageStatus.Cancelled;
                        log.FailureReason = "Opted out Email";
                        log.IsOptedOut = true;
                        failed++;
                        continue;
                    }
                }

                // Per-tenant rate limiting: simple delay, in real app use token bucket
                // already handled by batch delay

                // Retry with backoff: 3 attempts exponential 2^retry * 1s
                int maxRetries = 3;
                bool success = false;
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        log.RetryCount = attempt;
                        log.LastAttemptAt = DateTime.UtcNow;

                        if (batch.Channel == MessageChannel.Sms)
                        {
                            var smsProvider = await _providerFactory.GetSmsProviderAsync(batch.TenantId, ct);
                            var smsMsg = new SmsMessage { To = log.RecipientAddress, Body = log.RenderedBody, From = null };
                            var result = await smsProvider.SendAsync(smsMsg, ct);

                            if (result.Success)
                            {
                                log.Status = MessageStatus.Sent;
                                log.Provider = smsProvider.ProviderName;
                                log.ProviderReference = result.ProviderReference;
                                log.Cost = result.Cost;
                                log.Currency = result.Currency;
                                log.DeliveredAt = DateTime.UtcNow; // assume delivered for mock, real would be callback
                                success = true;
                                sent++;
                                delivered++;
                                actualCost += result.Cost;
                                break;
                            }
                            else
                            {
                                log.FailureReason = result.FailureReason;
                                if (attempt < maxRetries)
                                {
                                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                                    _logger.LogInformation("SMS send failed for {Recipient}, retry {Attempt} after {Backoff}s", log.RecipientAddress, attempt + 1, backoff.TotalSeconds);
                                    await Task.Delay(backoff, ct);
                                }
                            }
                        }
                        else // Email
                        {
                            var emailProvider = await _providerFactory.GetEmailProviderAsync(batch.TenantId, ct);
                            var emailMsg = new EmailMessage { To = log.RecipientAddress, Subject = log.RenderedSubject ?? batch.Subject ?? "Notice", HtmlBody = log.RenderedBody };
                            var result = await emailProvider.SendAsync(emailMsg, ct);

                            if (result.Success)
                            {
                                log.Status = MessageStatus.Sent;
                                log.Provider = emailProvider.ProviderName;
                                log.ProviderReference = result.ProviderReference;
                                log.Cost = result.Cost;
                                log.Currency = result.Currency;
                                log.DeliveredAt = DateTime.UtcNow;
                                success = true;
                                sent++;
                                delivered++;
                                actualCost += result.Cost;
                                break;
                            }
                            else
                            {
                                log.FailureReason = result.FailureReason;
                                if (attempt < maxRetries)
                                {
                                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                                    await Task.Delay(backoff, ct);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.FailureReason = ex.Message;
                        _logger.LogError(ex, "Exception sending message {LogId} attempt {Attempt}", log.Id, attempt);
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                        }
                    }
                }

                if (!success)
                {
                    log.Status = MessageStatus.Failed;
                    failed++;
                }

                await _db.SaveChangesAsync(ct);
            }

            // Update batch progress
            batch.SentCount = sent;
            batch.DeliveredCount = delivered;
            batch.FailedCount = failed;
            batch.ActualCost = Math.Round(actualCost, 2, MidpointRounding.AwayFromZero);
            batch.ProgressPercent = batch.TotalRecipients > 0 ? (int)((double)(sent + failed) / batch.TotalRecipients * 100) : 100;
            await _db.SaveChangesAsync(ct);

            // Rate limit delay between batches
            await Task.Delay(delayBetweenBatches, ct);

            // Update usage counter for billing SMS bundles (per-tenant)
            var now = DateTime.UtcNow;
            var usage = await _db.Set<TenantMessagingUsage>().FirstOrDefaultAsync(u => u.TenantId == batch.TenantId && u.Year == now.Year && u.Month == now.Month && !u.IsDeleted, ct);
            if (usage == null)
            {
                usage = new TenantMessagingUsage { TenantId = batch.TenantId, Year = now.Year, Month = now.Month, SmsLimit = providerSettings?.DailyCap ?? 1000, EmailLimit = 5000 };
                _db.Set<TenantMessagingUsage>().Add(usage);
            }

            if (batch.Channel == MessageChannel.Sms)
            {
                usage.SmsCount += chunk.Count(c => c.Status == MessageStatus.Sent);
                usage.SmsCost = Math.Round(usage.SmsCost + chunk.Where(c=>c.Status==MessageStatus.Sent).Sum(c=>c.Cost), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                usage.EmailCount += chunk.Count(c => c.Status == MessageStatus.Sent);
                usage.EmailCost = Math.Round(usage.EmailCost + chunk.Where(c=>c.Status==MessageStatus.Sent).Sum(c=>c.Cost), 2, MidpointRounding.AwayFromZero);
            }
            await _db.SaveChangesAsync(ct);
        }

        batch.Status = failed == batch.TotalRecipients ? MessageStatus.Failed : MessageStatus.Sent;
        batch.CompletedAt = DateTime.UtcNow;
        batch.ProgressPercent = 100;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Batch {BatchId} completed: sent {Sent}, failed {Failed}, cost {Cost}", batchId, sent, failed, actualCost);
    }
}
