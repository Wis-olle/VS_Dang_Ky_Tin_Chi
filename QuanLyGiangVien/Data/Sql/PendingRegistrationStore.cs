using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuanLyGiangVien.Domain.Registration;

namespace QuanLyGiangVien.Data.Sql;

public sealed record PendingRegistration(Guid RequestId, string Term, ImmutableArray<string> Add,
    ImmutableArray<long> Remove, byte[]? CartVersion);

public enum PendingRegistrationFailure { StorageUnavailable, CorruptPayload, ConflictingRequest }

public sealed class PendingRegistrationStoreException(string message, PendingRegistrationFailure kind)
    : InvalidOperationException(message)
{
    public PendingRegistrationFailure Kind { get; } = kind;
}

/// <summary>No credentials. One unresolved request per identity/term, protected across application processes.</summary>
public sealed class PendingRegistrationStore
{
    private readonly string path;
    public PendingRegistrationStore(string directory, string identityScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityScope);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityScope)));
        path = Path.Combine(Path.GetFullPath(directory), key + ".json");
    }
    public PendingRegistration? Load() => Guard(() =>
    {
        using var gate = AcquireLock();
        return ReadLocked();
    });
    public void Save(PendingRegistration request)
    {
        var snapshot = Canonical(request);
        Guard(() =>
        {
            using var gate = AcquireLock();
            var existing = ReadLocked();
            if (existing != null)
            {
                if (!Same(existing, snapshot)) throw Conflict();
                return true;
            }
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes); file.Flush(flushToDisk: true); }
                // No overwrite: even an unexpected writer cannot replace an unresolved request.
                File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        });
    }
    public void Clear(Guid expectedRequestId)
    {
        RegistrationWorkflowValidation.Request(expectedRequestId);
        Guard(() =>
        {
            using var gate = AcquireLock();
            var existing = ReadLocked();
            if (existing == null) return true;
            if (existing.RequestId != expectedRequestId) throw Conflict();
            File.Delete(path); return true;
        });
    }
    private FileStream AcquireLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (timer.ElapsedMilliseconds < 1000) { Thread.Sleep(20); }
        }
    }
    private PendingRegistration? ReadLocked()
    {
        if (!File.Exists(path)) return null;
        try
        {
            var request = JsonSerializer.Deserialize<PendingRegistration>(File.ReadAllText(path));
            return Canonical(request ?? throw new JsonException());
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new PendingRegistrationStoreException("Không đọc được yêu cầu đang chờ. Giữ nguyên tệp theo dõi và liên hệ quản trị để đối soát trước khi gửi mới.", PendingRegistrationFailure.CorruptPayload);
        }
    }
    private static PendingRegistration Canonical(PendingRegistration request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RegistrationWorkflowValidation.Request(request.RequestId);
        if (request.Add.IsDefault || request.Remove.IsDefault) throw new ArgumentException("Nội dung yêu cầu đang chờ không hợp lệ.");
        var additions = JsonSerializer.Deserialize<ImmutableArray<string>>(RegistrationWorkflowValidation.ClassesJson(request.Add));
        var removals = JsonSerializer.Deserialize<ImmutableArray<long>>(RegistrationWorkflowValidation.RemovalsJson(request.Remove));
        if (additions.IsEmpty && removals.IsEmpty) throw new ArgumentException("Yêu cầu đang chờ chưa có thay đổi.");
        return new(request.RequestId, RegistrationValidation.RequiredCode(request.Term).ToUpperInvariant(), additions,
            removals, RegistrationWorkflowValidation.Version(request.CartVersion));
    }
    private static bool Same(PendingRegistration first, PendingRegistration second) =>
        first.RequestId == second.RequestId && first.Term == second.Term && first.Add.SequenceEqual(second.Add)
        && first.Remove.SequenceEqual(second.Remove) && ((first.CartVersion == null && second.CartVersion == null)
            || first.CartVersion != null && second.CartVersion != null && first.CartVersion.SequenceEqual(second.CartVersion));
    private static PendingRegistrationStoreException Conflict() => new(
        "Có yêu cầu đang chờ khác trong phiên ứng dụng khác. Tải lại và đối soát yêu cầu đó trước khi tiếp tục.", PendingRegistrationFailure.ConflictingRequest);
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new PendingRegistrationStoreException("Không thể khóa hoặc lưu tệp theo dõi yêu cầu. Chưa gửi yêu cầu mới; hãy thử lại sau khi kiểm tra quyền lưu dữ liệu.", PendingRegistrationFailure.StorageUnavailable);
        }
    }
}
