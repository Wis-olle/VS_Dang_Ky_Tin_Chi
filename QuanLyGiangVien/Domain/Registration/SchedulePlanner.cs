using System.Collections.Immutable;
using System.Diagnostics;
namespace QuanLyGiangVien.Domain.Registration;

/// <summary>Bounded, deterministic suggestion search. SQL precheck remains authoritative for academics and live seats.</summary>
public static class SchedulePlanner
{
    private sealed record Candidate(string Course, ImmutableArray<string> Ids, ImmutableArray<SchedulePlannerMeeting> Meetings);
    public static SchedulePlannerResult Generate(SchedulePlannerSnapshot snapshot, SchedulePlannerRequest request,
        CancellationToken cancellationToken = default)
        => Search(snapshot,request,request.MaxOptions,false,cancellationToken);
    internal static SchedulePlannerResult GenerateCandidates(SchedulePlannerSnapshot snapshot,SchedulePlannerRequest request,
        CancellationToken cancellationToken=default)=>Search(snapshot,request,Math.Min(100,request.MaxOptions*10),true,cancellationToken);
    private static SchedulePlannerResult Search(SchedulePlannerSnapshot snapshot,SchedulePlannerRequest request,int candidateLimit,bool validationPool,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Validate(request);
        var timer = Stopwatch.StartNew();
        var classes = snapshot.Classes.ToImmutableDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var fixedIds = request.FixedClassIds.Select(Code).Distinct().ToImmutableArray();
        if (fixedIds.Any(id => !classes.ContainsKey(id) || classes[id].Term != snapshot.Term))
            throw new ArgumentException("Lớp muốn giữ không thuộc học kỳ hoặc không còn trong dữ liệu đã tải.");
        var courses = request.CourseIds.Select(Code).Concat(fixedIds.Select(id => classes[id].CourseId))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToImmutableArray();
        if (courses.Length > 12) throw new ArgumentException("Mỗi lần sinh tối đa 12 học phần.");
        var existing = snapshot.Classes.Where(c => c.IsRegistered).ToImmutableArray();
        var baseline = existing.Where(c => c.BlocksSchedule).SelectMany(c => c.Meetings).ToImmutableArray();
        var errors = ImmutableArray.CreateBuilder<string>();
        if (Busy(baseline, request.BusyTimes)) errors.Add("Khung giờ bận trùng lịch đã đăng ký; bộ sinh không tự hủy lớp hiện tại.");
        var choices = new List<ImmutableArray<Candidate>>();
        foreach (var course in courses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(timer.ElapsedMilliseconds>=request.MaxMilliseconds)
                return new([], ["Đã chạm giới hạn thời gian khi chuẩn bị tổ hợp lớp; chưa thể kết luận không có phương án."],0,true,snapshot.CartVersion);
            var kept = existing.Where(c => c.Term == snapshot.Term && c.CourseId.Equals(course, StringComparison.OrdinalIgnoreCase)).ToImmutableArray();
            var required = fixedIds.Where(id => classes[id].CourseId.Equals(course, StringComparison.OrdinalIgnoreCase)).ToImmutableArray();
            ImmutableArray<Candidate> options;
            if (!kept.IsEmpty)
            {
                var ids = kept.Select(c => c.Id).Order().ToImmutableArray();
                options = required.All(ids.Contains) ? [new(course, ids, [])] : [];
            }
            else options = Candidates(snapshot, classes, course,cancellationToken,()=>timer.ElapsedMilliseconds<request.MaxMilliseconds).Where(c => required.All(c.Ids.Contains))
                .Where(c => !Busy(c.Meetings, request.BusyTimes) && !Conflict(c.Meetings, baseline))
                .OrderBy(c => string.Join("|", c.Ids), StringComparer.Ordinal).ToImmutableArray();
            if (options.IsEmpty) errors.Add($"{course}: không có lớp/tổ hợp còn khả dụng thỏa lớp muốn giữ, giờ bận và lịch hiện tại; có thể thiếu lịch, đủ chỗ hoặc trùng lịch.");
            choices.Add(options);
        }
        if(timer.ElapsedMilliseconds>=request.MaxMilliseconds)
            return new([], ["Đã chạm giới hạn thời gian khi chuẩn bị tổ hợp lớp; chưa thể kết luận không có phương án."],0,true,snapshot.CartVersion);
        if (errors.Count > 0) return new([], errors.ToImmutable(), 0, false,snapshot.CartVersion);
        var ordered = choices.OrderBy(c => c.Length).ThenBy(c => c[0].Course, StringComparer.Ordinal).ToArray();
        var best = new List<SchedulePlannerOption>(); var nodes = 0; var limited = false;var poolLimited=false;
        void Visit(int depth, ImmutableArray<string> ids, ImmutableArray<SchedulePlannerMeeting> meetings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nodes >= request.MaxNodes || timer.ElapsedMilliseconds >= request.MaxMilliseconds) { limited = true; return; }
            nodes++;
            if (depth == ordered.Length)
            {
                var option = Score(snapshot, classes, existing, ids, meetings, request.Preferences);
                if (best.All(p => p.Key != option.Key)) best.Add(option);
                best.Sort((a,b) => Compare(a,b,request.Preferences));
                if (best.Count > candidateLimit) {best.RemoveAt(best.Count - 1);poolLimited|=validationPool;}
                return;
            }
            foreach (var candidate in ordered[depth])
            {
                if (!Conflict(candidate.Meetings, meetings)) Visit(depth + 1, ids.AddRange(candidate.Ids), meetings.AddRange(candidate.Meetings));
                if (limited) return;
            }
        }
        Visit(0, [], baseline);
        if (best.Count == 0) errors.Add("Không tìm được phương án: các lớp/tổ hợp trùng lịch với nhau hoặc hết giới hạn tìm kiếm.");
        if (limited) errors.Add("Đã chạm giới hạn số nút hoặc thời gian; kết quả là các phương án đã tìm thấy, chưa phải toàn bộ không gian tìm kiếm.");
        if(poolLimited)errors.Add("Đã giới hạn số phương án đưa qua kiểm tra học vụ; có thể còn phương án ngoài tập được kiểm tra.");
        return new(best.ToImmutableArray(), errors.ToImmutable(), nodes, limited||poolLimited,snapshot.CartVersion);
    }
    private static IEnumerable<Candidate> Candidates(SchedulePlannerSnapshot snapshot,
        ImmutableDictionary<string,SchedulePlannerClass> classes, string course,CancellationToken token,Func<bool> withinBudget)
    {
        var bundles = snapshot.Bundles.Where(b => b.CourseId.Equals(course, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (bundles.Length > 0)
        {
            foreach (var bundle in bundles)
            {
                token.ThrowIfCancellationRequested();if(!withinBudget())yield break;
                if (!ValidBundle(bundle) || bundle.ClassIds.Any(id => !classes.TryGetValue(id, out var c) || !c.CanAdd || c.Term != snapshot.Term || c.CourseId != course || c.Meetings.IsEmpty)) continue;
                var meetings = bundle.ClassIds.SelectMany(id => classes[id].Meetings).ToImmutableArray();
                if (!SelfConflict(meetings)) yield return new(course, bundle.ClassIds.Order().ToImmutableArray(), meetings);
            }
        }
        else foreach (var c in snapshot.Classes.Where(c => c.CourseId == course && c.Term == snapshot.Term && c.CanAdd && !c.Meetings.IsEmpty))
        {token.ThrowIfCancellationRequested();if(!withinBudget())yield break;if (!SelfConflict(c.Meetings)) yield return new(course, [c.Id], c.Meetings);}
    }
    private static bool ValidBundle(SchedulePlannerBundle bundle) => bundle.ClassIds.Length is >=2 and <=3
        && bundle.Roles.Length == bundle.ClassIds.Length && bundle.ClassIds.Distinct().Count() == bundle.ClassIds.Length
        && bundle.Roles.Count(r => r == "LT") == 1 && bundle.Roles.Any(r => r is "TH" or "TL")
        && bundle.Roles.All(r => r is "LT" or "TH" or "TL") && bundle.Roles.Distinct().Count() == bundle.Roles.Length;
    private static bool SelfConflict(ImmutableArray<SchedulePlannerMeeting> meetings) => meetings.GroupBy(m => m.Date)
        .Any(day => day.OrderBy(m => m.StartPeriod).Zip(day.OrderBy(m => m.StartPeriod).Skip(1)).Any(pair => pair.First.EndPeriod >= pair.Second.StartPeriod));
    private static bool Conflict(ImmutableArray<SchedulePlannerMeeting> left, ImmutableArray<SchedulePlannerMeeting> right)
    {
        if (left.IsEmpty || right.IsEmpty) return false;
        var byDate = right.ToLookup(m => m.Date);
        return left.Any(a => byDate[a.Date].Any(b => a.StartPeriod <= b.EndPeriod && b.StartPeriod <= a.EndPeriod));
    }
    private static bool Busy(ImmutableArray<SchedulePlannerMeeting> meetings, ImmutableArray<SchedulePlannerBusy> times) =>
        meetings.Any(m => times.Any(t => m.Date >= t.From && m.Date <= t.Until && m.Date.DayOfWeek == t.Day && m.StartPeriod <= t.EndPeriod && t.StartPeriod <= m.EndPeriod));
    private static SchedulePlannerOption Score(SchedulePlannerSnapshot snapshot, ImmutableDictionary<string,SchedulePlannerClass> classes,
        ImmutableArray<SchedulePlannerClass> existing, ImmutableArray<string> selected, ImmutableArray<SchedulePlannerMeeting> meetings,
        SchedulePlannerPreferences preferences)
    {
        var own = existing.Select(c => c.Id).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        var all = selected.Concat(own).Distinct().Order().ToImmutableArray();
        var additions = selected.Where(id => !own.Contains(id)).Distinct().Order().ToImmutableArray();
        var currentCourses = all.Where(id => classes[id].Term == snapshot.Term).Select(id => classes[id])
            .GroupBy(c => c.CourseId).Select(g => g.First());
        var dates = meetings.GroupBy(m => m.Date).ToArray();
        var days = dates.GroupBy(d => d.Key.AddDays(-((int)d.Key.DayOfWeek + 6) % 7)).Select(w => w.Count()).DefaultIfEmpty().Max();
        var gaps = dates.Sum(d => {var sorted=d.OrderBy(m=>m.StartPeriod).ToArray();return sorted.Zip(sorted.Skip(1)).Sum(p=>Math.Max(0,p.Second.StartPeriod-p.First.EndPeriod-1));});
        var time = preferences.Time switch {ScheduleTimePreference.Early => meetings.Sum(m=>m.StartPeriod-1),ScheduleTimePreference.Late => meetings.Sum(m=>20-m.EndPeriod),_=>0};
        return new(string.Join("|",all), additions, all, meetings.OrderBy(m=>m.Date).ThenBy(m=>m.StartPeriod).ThenBy(m=>m.ClassId).ToImmutableArray(),
            currentCourses.Sum(c=>c.Credits),days,gaps,time,[]);
    }
    private static int Compare(SchedulePlannerOption a,SchedulePlannerOption b,SchedulePlannerPreferences p)
    {
        var result=(p.MinimizeDays?a.DaysPerWeek:0).CompareTo(p.MinimizeDays?b.DaysPerWeek:0);
        if(result==0)result=(p.MinimizeGaps?a.GapPeriods:0).CompareTo(p.MinimizeGaps?b.GapPeriods:0);
        if(result==0)result=a.TimePenalty.CompareTo(b.TimePenalty);
        return result==0?StringComparer.Ordinal.Compare(a.Key,b.Key):result;
    }
    private static string Code(string value)=>RegistrationValidation.RequiredCode(value).ToUpperInvariant();
    public static void Validate(SchedulePlannerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.CourseIds.IsDefault||request.FixedClassIds.IsDefault||request.BusyTimes.IsDefault||request.Preferences is null
            ||request.CourseIds.Length>12||request.FixedClassIds.Length>30||request.BusyTimes.Length>30
            ||request.CourseIds.IsEmpty&&request.FixedClassIds.IsEmpty||request.MaxOptions is <1 or >10
            ||request.MaxNodes is <1 or >100000||request.MaxMilliseconds is <50 or >10000||!Enum.IsDefined(request.Preferences.Time))
            throw new ArgumentException("Chọn 1–12 môn, tối đa 30 lớp/giờ bận; tối đa 10 phương án, 100.000 nút và 10 giây.");
        foreach(var id in request.CourseIds.Concat(request.FixedClassIds)) Code(id);
        if(request.BusyTimes.Any(t=>t.Until<t.From||!Enum.IsDefined(t.Day)||t.StartPeriod<1||t.EndPeriod>20||t.StartPeriod>t.EndPeriod))
            throw new ArgumentException("Khung giờ bận phải có khoảng ngày và tiết 1–20 hợp lệ.");
    }
}
