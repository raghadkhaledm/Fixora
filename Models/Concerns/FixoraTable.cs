namespace Fixora.Models.Concerns;

public interface FixoraTable
{
    string GetDateFormat()
    {
        return "yyyy-MM-dd HH:mm:ss";
    }

    bool UsesTimestamps()
    {
        return false;
    }
}
