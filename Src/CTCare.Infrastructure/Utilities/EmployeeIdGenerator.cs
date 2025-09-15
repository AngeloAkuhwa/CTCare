using CTCare.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Utilities;

public static class EmployeeIdGenerator
{
    public static async Task<string> GenerateAsync(CtCareDbContext db)
    {
        var last = await db.Employees
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.EmployeeCode)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.StartsWith("CN"))
        {
            if (int.TryParse(last[2..], out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        return $"CN{nextNumber}";
    }
}
