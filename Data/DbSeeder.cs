using Microsoft.EntityFrameworkCore;

namespace Fixora.Data;

public class DbSeeder
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public DbSeeder(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public void Run()
    {
        var script = _configuration["Seeding:InsertScript"];
        if (string.IsNullOrEmpty(script) || !File.Exists(script))
        {
            return;
        }

        if (_db.Admins.Any())
        {
            return;
        }

        var sql = File.ReadAllText(script);
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(sql, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                _db.Database.ExecuteSqlRaw(batch);
            }
        }
    }
}
