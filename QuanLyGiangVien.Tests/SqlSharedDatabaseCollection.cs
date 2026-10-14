using Xunit;

namespace QuanLyGiangVien.Tests;

// These tests compare multiple reads and create temporary catalogs in the same
// MINH_DB_TEST_CONNECTION fixture. Keep them isolated from each other's writes.
[CollectionDefinition("Shared SQL fixture")]
public sealed class SqlSharedDatabaseCollection;
