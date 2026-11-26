using Fixora.Models;

namespace Fixora.Data;

public static class RelationCounts
{
    public static IEnumerable<Technician> WithCounts(this AppDbContext db, IEnumerable<Technician> technicians)
    {
        var list = technicians.ToList();
        var ids = list.Select(t => t.Technician_ID).ToList();
        var reviews = db.Reviews.Where(r => ids.Contains(r.Technician_ID)).GroupBy(r => r.Technician_ID).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionary(x => x.Id, x => x.N);
        var jobs = db.Bookings.Where(b => ids.Contains(b.Technician_ID) && b.Status == "Completed").GroupBy(b => b.Technician_ID).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionary(x => x.Id, x => x.N);
        foreach (var t in list)
        {
            t.Reviews_Count = reviews.TryGetValue(t.Technician_ID, out var r) ? r : 0;
            t.Jobs_Count = jobs.TryGetValue(t.Technician_ID, out var j) ? j : 0;
        }

        return list;
    }

    public static Technician WithCounts(this AppDbContext db, Technician technician)
    {
        db.WithCounts(new[] { technician }).ToList();

        return technician;
    }

    public static IEnumerable<HomeOwner> WithCounts(this AppDbContext db, IEnumerable<HomeOwner> owners)
    {
        var list = owners.ToList();
        var ids = list.Select(o => o.Home_owner_ID).ToList();
        var all = db.Bookings.Where(b => ids.Contains(b.Home_owner_ID)).GroupBy(b => b.Home_owner_ID).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionary(x => x.Id, x => x.N);
        var done = db.Bookings.Where(b => ids.Contains(b.Home_owner_ID) && b.Status == "Completed").GroupBy(b => b.Home_owner_ID).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionary(x => x.Id, x => x.N);
        foreach (var o in list)
        {
            o.Bookings_Count = all.TryGetValue(o.Home_owner_ID, out var a) ? a : 0;
            o.Completed_Count = done.TryGetValue(o.Home_owner_ID, out var d) ? d : 0;
        }

        return list;
    }
}
