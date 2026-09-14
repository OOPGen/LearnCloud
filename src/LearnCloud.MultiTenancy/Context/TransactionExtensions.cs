using Microsoft.EntityFrameworkCore;

namespace LearnCloud.MultiTenancy.Context;

public static class TransactionExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction under the context's
    /// retrying execution strategy.
    /// </summary>
    /// <remarks>
    /// The context enables automatic retries for transient PostgreSQL failures. EF Core
    /// rejects a manual BeginTransaction outside the execution strategy, which is why
    /// school registration and attendance marking failed on every call. On a retry the
    /// whole delegate runs again from a cleared change tracker, so it must load whatever
    /// it modifies itself rather than reuse entities loaded before the call.
    /// </remarks>
    public static Task<T> InTransactionAsync<T>(this LearnCloudDbContext db, Func<Task<T>> work, CancellationToken ct = default)
    {
        var attempt = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}
