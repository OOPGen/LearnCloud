using LearnCloud.Core.DTOs;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.Core.Services;

public interface IGuardianService
{
    Task<PagedResult<GuardianListItemDto>> ListAsync(long tenantId, GuardianListRequest req, CancellationToken ct = default);
    Task<GuardianDetailDto> GetAsync(long tenantId, long guardianId, CancellationToken ct = default);
    Task<GuardianDetailDto> CreateAsync(long tenantId, long userId, GuardianInput req, CancellationToken ct = default);
    Task<GuardianDetailDto> UpdateAsync(long tenantId, long userId, long guardianId, GuardianInput req, CancellationToken ct = default);
    Task DeleteAsync(long tenantId, long userId, long guardianId, CancellationToken ct = default);
}

public class GuardianService : IGuardianService
{
    private readonly LearnCloudDbContext _db;

    public GuardianService(LearnCloudDbContext db) => _db = db;

    public async Task<PagedResult<GuardianListItemDto>> ListAsync(long tenantId, GuardianListRequest req, CancellationToken ct = default)
    {
        var query = _db.Set<Guardian>().Where(g => g.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(g => g.FirstName.ToLower().Contains(term) || g.LastName.ToLower().Contains(term)
                || (g.FirstName + " " + g.LastName).ToLower().Contains(term) || g.Phone.Contains(term) || (g.Email != null && g.Email.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(g => g.LastName).ThenBy(g => g.FirstName).ThenBy(g => g.Id)
            .Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .Select(g => new GuardianListItemDto(g.Id, g.FirstName, g.LastName, g.Phone, g.Email,
                _db.Set<GuardianStudentLink>().Count(l => l.TenantId == tenantId && l.GuardianId == g.Id), g.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<GuardianListItemDto>(items, total, req.Page, req.PageSize, (int)Math.Ceiling(total / (double)req.PageSize));
    }

    public async Task<GuardianDetailDto> GetAsync(long tenantId, long guardianId, CancellationToken ct = default)
    {
        var g = await FindAsync(tenantId, guardianId, ct);
        var students = await (from l in _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.GuardianId == guardianId)
                              join s in _db.Set<Student>().Where(s => s.TenantId == tenantId) on l.StudentId equals s.Id
                              join gr in _db.Set<Grade>().Where(x => x.TenantId == tenantId) on s.GradeId equals gr.Id
                              join st in _db.Set<ClassStream>().Where(x => x.TenantId == tenantId) on s.StreamId equals st.Id
                              orderby s.LastName, s.FirstName
                              select new GuardianStudentDto(l.Id, s.Id, s.StudentNumber, s.FirstName, s.LastName, s.Status, gr.Name + " " + st.Name,
                                  l.RelationshipType, l.IsPrimaryContact, l.IsBillingContact, l.IsEmergencyContact, l.CanPickup))
                             .ToListAsync(ct);
        return new GuardianDetailDto(g.Id, g.FirstName, g.LastName, g.Phone, g.Email, g.Address, g.NationalId, students, g.CreatedAt);
    }

    public async Task<GuardianDetailDto> CreateAsync(long tenantId, long userId, GuardianInput req, CancellationToken ct = default)
    {
        var guardian = NewGuardian(tenantId, userId, req);
        if (await _db.Set<Guardian>().AnyAsync(g => g.TenantId == tenantId && g.Phone == guardian.Phone && g.FirstName == guardian.FirstName && g.LastName == guardian.LastName, ct))
            throw new InvalidOperationException($"{guardian.FirstName} {guardian.LastName} with phone {guardian.Phone} already exists.");

        _db.Set<Guardian>().Add(guardian);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, guardian.Id, ct);
    }

    public async Task<GuardianDetailDto> UpdateAsync(long tenantId, long userId, long guardianId, GuardianInput req, CancellationToken ct = default)
    {
        var guardian = await FindAsync(tenantId, guardianId, ct);
        var values = NewGuardian(tenantId, userId, req);
        guardian.FirstName = values.FirstName;
        guardian.LastName = values.LastName;
        guardian.Phone = values.Phone;
        guardian.Email = values.Email;
        guardian.Address = values.Address;
        guardian.NationalId = values.NationalId;
        guardian.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, guardianId, ct);
    }

    public async Task DeleteAsync(long tenantId, long userId, long guardianId, CancellationToken ct = default)
    {
        var guardian = await FindAsync(tenantId, guardianId, ct);
        var linked = await _db.Set<GuardianStudentLink>().CountAsync(l => l.TenantId == tenantId && l.GuardianId == guardianId, ct);
        if (linked > 0)
            throw new InvalidOperationException($"{guardian.FirstName} {guardian.LastName} is linked to {linked} student{(linked == 1 ? "" : "s")}. Unlink them first.");

        guardian.DeletedBy = userId;
        _db.Set<Guardian>().Remove(guardian);
        await _db.SaveChangesAsync(ct);
    }

    internal static Guardian NewGuardian(long tenantId, long userId, GuardianInput input) => new()
    {
        TenantId = tenantId,
        FirstName = input.FirstName.Trim(),
        LastName = input.LastName.Trim(),
        Phone = input.Phone.Trim(),
        Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant(),
        Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim(),
        NationalId = string.IsNullOrWhiteSpace(input.NationalId) ? null : input.NationalId.Trim(),
        CreatedBy = userId,
    };

    private async Task<Guardian> FindAsync(long tenantId, long guardianId, CancellationToken ct) =>
        await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == guardianId, ct)
        ?? throw new InvalidOperationException("Guardian not found");
}
