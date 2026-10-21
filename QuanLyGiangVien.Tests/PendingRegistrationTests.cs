using System.Collections.Immutable;
using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class PendingRegistrationTests
{
    [Fact] public void PendingPayloadSurvivesNewStoreInstanceAndIsScopedByIdentity()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CampusPendingTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PendingRegistrationStore(dir, "server|database|student1|term");
            var request = new PendingRegistration(Guid.NewGuid(), "HK1", ["LT", "TH"], [42], null);
            store.Save(request);
            var loaded = new PendingRegistrationStore(dir, "server|database|student1|term").Load()!;
            Assert.Equal(request.RequestId, loaded.RequestId); Assert.Equal(request.Add.ToArray(), loaded.Add.ToArray());
            Assert.Equal(request.Remove.ToArray(), loaded.Remove.ToArray());
            Assert.Null(new PendingRegistrationStore(dir, "server|database|student2|term").Load());
            store.Clear(request.RequestId); Assert.Null(store.Load());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact] public void CorruptPendingPayloadFailsClosed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CampusPendingTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PendingRegistrationStore(dir, "identity");
            store.Save(new(Guid.NewGuid(), "HK1", ["LT"], [], null));
            File.WriteAllText(Directory.GetFiles(dir,"*.json").Single(), "broken");
            Assert.Throws<PendingRegistrationStoreException>(() => store.Load());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact] public void AnotherRequestCannotOverwriteOrClearUnresolvedPayload()
    {
        var dir=Path.Combine(Path.GetTempPath(),"CampusPendingTest_"+Guid.NewGuid().ToString("N"));
        try {
            var a=new PendingRegistrationStore(dir,"same");var b=new PendingRegistrationStore(dir,"same");
            var first=new PendingRegistration(Guid.NewGuid(),"HK1",["LT"],[],null);var second=first with{RequestId=Guid.NewGuid()};
            a.Save(first);Assert.Equal(PendingRegistrationFailure.ConflictingRequest,Assert.Throws<PendingRegistrationStoreException>(()=>b.Save(second)).Kind);
            Assert.Equal(PendingRegistrationFailure.ConflictingRequest,Assert.Throws<PendingRegistrationStoreException>(()=>b.Clear(second.RequestId)).Kind);
            Assert.Equal(first.RequestId,a.Load()!.RequestId);
            a.Clear(first.RequestId);Assert.Null(b.Load());
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Fact] public void SameRequestIdCannotChangeItsPayload()
    {
        var dir=Path.Combine(Path.GetTempPath(),"CampusPendingTest_"+Guid.NewGuid().ToString("N"));
        try {
            var store=new PendingRegistrationStore(dir,"same");var first=new PendingRegistration(Guid.NewGuid(),"HK1",["LT","TH"],[2,1],new byte[8]);
            store.Save(first);store.Save(first with{Add=["TH","LT"],Remove=[1,2]});
            Assert.Throws<PendingRegistrationStoreException>(()=>store.Save(first with{Add=["OTHER"]}));
            Assert.Equal(new[]{"LT","TH"},store.Load()!.Add.ToArray());
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Fact] public async Task ConcurrentStoreInstancesKeepExactlyOnePendingRequest()
    {
        var dir=Path.Combine(Path.GetTempPath(),"CampusPendingTest_"+Guid.NewGuid().ToString("N"));
        try {
            var requests=new[]{new PendingRegistration(Guid.NewGuid(),"HK1",["A"],[],null),new PendingRegistration(Guid.NewGuid(),"HK1",["B"],[],null)};
            using var start=new ManualResetEventSlim();
            var attempts=requests.Select(request=>Task.Run(()=>{start.Wait();try{new PendingRegistrationStore(dir,"scope").Save(request);return true;}catch(PendingRegistrationStoreException e)when(e.Kind==PendingRegistrationFailure.ConflictingRequest){return false;}})).ToArray();
            start.Set();var success=await Task.WhenAll(attempts);Assert.Single(success,v=>v);
            var saved=new PendingRegistrationStore(dir,"scope").Load()!;Assert.Equal(requests[Array.FindIndex(success,v=>v)].RequestId,saved.RequestId);
            Assert.Empty(Directory.GetFiles(dir,"*.tmp"));
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Fact] public void LockedOrUnwritableStorageFailsClosedWithSafeTypedError()
    {
        var dir=Path.Combine(Path.GetTempPath(),"CampusPendingTest_"+Guid.NewGuid().ToString("N"));
        try {
            var request=new PendingRegistration(Guid.NewGuid(),"HK1",["A"],[],null);var store=new PendingRegistrationStore(dir,"scope");store.Save(request);
            using(var held=new FileStream(Directory.GetFiles(dir,"*.lock").Single(),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                Assert.Equal(PendingRegistrationFailure.StorageUnavailable,Assert.Throws<PendingRegistrationStoreException>(()=>store.Save(request)).Kind);
            Assert.Equal(request.RequestId,store.Load()!.RequestId);
            var blocker=Path.Combine(dir,"file");File.WriteAllText(blocker,"fixture");
            var error=Assert.Throws<PendingRegistrationStoreException>(()=>new PendingRegistrationStore(blocker,"scope").Save(request));
            Assert.Equal(PendingRegistrationFailure.StorageUnavailable,error.Kind);Assert.DoesNotContain(dir,error.Message);
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
