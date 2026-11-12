using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace UselessPlayerInfo.Functions;

public enum JobRole
{
    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    MagicalRanged,
    LimitedJob,
    Hand,
    Land
}

internal static class Jobs
{
    private const byte RoleTank = 1;
    private const byte RoleMelee = 2;
    private const byte RoleRanged = 3;
    private const byte RoleHealer = 4;
    private const byte PrimaryStatDexterity = 2;
    private const byte PrimaryStatIntelligence = 4;
    private const uint CategoryDiscipleOfLand = 32;
    private const uint CategoryDiscipleOfHand = 33;

    private static IReadOnlyList<(string Abbrev, uint Id, JobRole Role)>? all;
    public static IReadOnlyList<(string Abbrev, uint Id, JobRole Role)> All => all ??= BuildJobList();

    private static List<(string Abbrev, uint Id, JobRole Role)> BuildJobList()
    {
        var jobs = new List<(string Abbrev, uint Id, JobRole Role, byte Priority)>();

        foreach (var classJob in Plugin.DataManager.GetExcelSheet<ClassJob>())
        {
            var role = GetRole(classJob);
            if (role == null)
            {
                continue;
            }

            jobs.Add((classJob.Abbreviation.ToString(), classJob.RowId, role.Value, classJob.UIPriority));
        }

        return jobs.OrderBy(job => job.Priority).Select(job => (job.Abbrev, job.Id, job.Role)).ToList();
    }

    private static JobRole? GetRole(ClassJob classJob)
    {
        switch (classJob.ClassJobCategory.RowId)
        {
            case CategoryDiscipleOfHand:
                return JobRole.Hand;
            case CategoryDiscipleOfLand:
                return JobRole.Land;
        }

        if (classJob.JobIndex == 0)
        {
            return null;
        }

        if (classJob.IsLimitedJob)
        {
            return JobRole.LimitedJob;
        }

        return classJob.Role switch
        {
            RoleTank => JobRole.Tank,
            RoleHealer => JobRole.Healer,
            RoleMelee => JobRole.Melee,
            RoleRanged when classJob.PrimaryStat == PrimaryStatDexterity => JobRole.PhysicalRanged,
            RoleRanged when classJob.PrimaryStat == PrimaryStatIntelligence => JobRole.MagicalRanged,
            _ => null,
        };
    }

    private const uint JobIconBaseId = 62100;

    public static uint GetIconId(uint jobId) => JobIconBaseId + jobId;

    public static List<(string Abbrev, int Level)> GetUnlockedJobLevels()
    {
        var result = new List<(string Abbrev, int Level)>();

        foreach (var (abbrev, id, _) in All)
        {
            if (!Plugin.DataManager.GetExcelSheet<ClassJob>().TryGetRow(id, out var classJob))
            {
                continue;
            }

            if (!Plugin.UnlockState.IsClassJobUnlocked(classJob))
            {
                continue;
            }

            var level = Plugin.PlayerState.GetClassJobLevel(classJob);
            if (level <= 0)
            {
                continue;
            }

            result.Add((abbrev, level));
        }

        return result;
    }
}
