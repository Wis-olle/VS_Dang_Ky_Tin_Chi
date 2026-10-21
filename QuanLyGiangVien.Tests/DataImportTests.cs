using System.Text.Json;
using QuanLyGiangVien.Domain.Operations;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class DataImportTests
{
    private static string Document(string rows, string source = "Hồ sơ đã xác nhận") =>
        $$"""{"batchId":"00000000-0000-0000-0000-000000000001","source":{{JsonSerializer.Serialize(source)}},"rows":{{rows}}} """;
    [Fact] public void ParsesOrderedRowsAndKeepsSource() {
        var batch = DataImport.Parse(Document("""[{"kind":"PROGRAM","id":"CTDT1","payload":{"name":"CNTT"}}]"""));
        Assert.Equal("CTDT1", batch.Rows[0].Id); Assert.Equal("Hồ sơ đã xác nhận", batch.Source);
        Assert.Equal(0, batch.Rows[0].Index);
    }
    [Theory] [InlineData("")] [InlineData("{bad}")] [InlineData("[]")]
    public void RejectsMalformedDocument(string text) => Assert.Throws<ArgumentException>(() => DataImport.Parse(text));
    [Theory] [InlineData("")] [InlineData(" ")]
    public void RequiresConfirmedSourceReference(string source) => Assert.Throws<ArgumentException>(() => DataImport.Parse(Document("""[{"kind":"RESULT","id":"R1","payload":{}}]""", source)));
    [Theory] [InlineData("ACCOUNT")] [InlineData("OVERRIDE")] [InlineData("HOLD")]
    public void RejectsUnsupportedOrSecuritySensitiveKinds(string kind) => Assert.Throws<ArgumentException>(() => DataImport.Parse(Document(JsonSerializer.Serialize(new[] { new { kind, id = "R1", payload = new { } } }))));
    [Fact] public void CollectsAllBoundaryErrorsByRow() {
        var errors = DataImport.Inspect(Document("""[{"kind":"RESULT","id":"bad quote'","payload":{}},{"kind":"STUDENT","id":"SV1","payload":[]} ]"""));
        Assert.Equal(new[] { 0, 1 }, errors.Select(e => e.Row));
    }
    [Fact] public void RejectsDuplicateIdentityAndDoesNotMutateInput() {
        var text = Document("""[{"kind":"PROGRAM","id":"P1","payload":{}},{"kind":"PROGRAM","id":"P1","payload":{}}]""");
        Assert.Contains(DataImport.Inspect(text), e => e.Row == 1 && e.Message.Contains("trùng"));
        Assert.Throws<ArgumentException>(() => DataImport.Parse(text));
    }
    [Fact] public void AllowsOnlyCorrectHexVersion() {
        var errors = DataImport.Inspect(Document("""[{"kind":"RESULT","id":"R1","version":"bad","payload":{}}]"""));
        Assert.Single(errors); Assert.Contains("phiên bản", errors[0].Message);
    }
    [Fact] public void CapsRowsAndDocumentSize() {
        Assert.Throws<ArgumentException>(() => DataImport.Parse(Document("[" + string.Join(',', Enumerable.Repeat("{\"kind\":\"RESULT\",\"id\":\"R1\",\"payload\":{}}", 101)) + "]")));
        Assert.Throws<ArgumentException>(() => DataImport.Parse(new string(' ', 2_000_001)));
    }
    [Fact] public void EmptyBatchAndGuidFailClosed() {
        Assert.Throws<ArgumentException>(() => DataImport.Parse(Document("[]")));
        Assert.Throws<ArgumentException>(() => DataImport.Parse(Document("""[{"kind":"RESULT","id":"R1","payload":{}}]""").Replace("00000000-0000-0000-0000-000000000001", Guid.Empty.ToString())));
    }
    [Fact] public void TemplateIsUncommittableUntilFilledWithRealData() {
        using var json = JsonDocument.Parse(DataImport.Template("RESULT"));
        Assert.NotEqual(Guid.Empty, json.RootElement.GetProperty("batchId").GetGuid());
        Assert.Equal("RESULT", json.RootElement.GetProperty("rows")[0].GetProperty("kind").GetString());
        Assert.Throws<ArgumentException>(() => DataImport.Parse(json.RootElement.GetRawText()));
    }
}
