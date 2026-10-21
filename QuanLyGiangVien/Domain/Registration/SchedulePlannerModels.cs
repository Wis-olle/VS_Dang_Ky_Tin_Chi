using System.Collections.Immutable;
namespace QuanLyGiangVien.Domain.Registration;

public enum ScheduleTimePreference { Neutral, Early, Late }
public sealed record SchedulePlannerPreferences(bool MinimizeDays = true, bool MinimizeGaps = true,
    ScheduleTimePreference Time = ScheduleTimePreference.Neutral);
public sealed record SchedulePlannerBusy(DateOnly From, DateOnly Until, DayOfWeek Day, int StartPeriod, int EndPeriod);
public sealed record SchedulePlannerMeeting(string ClassId, DateOnly Date, int StartPeriod, int EndPeriod);
public sealed record SchedulePlannerClass(string Id, string CourseId, string CourseName, int Credits, string Term,
    bool CanAdd, bool IsRegistered, bool BlocksSchedule, ImmutableArray<SchedulePlannerMeeting> Meetings);
public sealed record SchedulePlannerBundle(string Id, string CourseId, ImmutableArray<string> ClassIds,
    ImmutableArray<string> Roles);
public sealed record SchedulePlannerSnapshot(string Term, ImmutableArray<SchedulePlannerClass> Classes,
    ImmutableArray<SchedulePlannerBundle> Bundles, string? CartVersion, ImmutableArray<string> CartClassIds = default);
public sealed record SchedulePlannerRequest(ImmutableArray<string> CourseIds, ImmutableArray<string> FixedClassIds,
    ImmutableArray<SchedulePlannerBusy> BusyTimes, SchedulePlannerPreferences Preferences,
    int MaxNodes = 10000, int MaxMilliseconds = 3000, int MaxOptions = 5);
public sealed record SchedulePlannerOption(string Key, ImmutableArray<string> AddClassIds,
    ImmutableArray<string> AllClassIds, ImmutableArray<SchedulePlannerMeeting> Meetings, int TotalCredits,
    int DaysPerWeek, int GapPeriods, int TimePenalty, ImmutableArray<string> Warnings);
public sealed record SchedulePlannerResult(ImmutableArray<SchedulePlannerOption> Options,
    ImmutableArray<string> Explanations, int ExploredNodes, bool Truncated, string? CartVersion = null);
