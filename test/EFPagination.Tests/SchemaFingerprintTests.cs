using System.Reflection;
using System.Reflection.Emit;
using EFPagination.TestModels;
using FluentAssertions;
using Xunit;

namespace EFPagination;

public class SchemaFingerprintTests
{
    // Issued by separate test processes.
    private const string IdCursor = "BQBBbX1gAQAKAAAA";
    private const string CreatedNullableDescendingThenIdCursor = "BQB7ccBLAgAAAPi0yEjeCAEKAAAA";

    [Fact]
    public void TryDecode_WithMatchingFingerprint_Succeeds()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(def, new MainModel { Id = 10 });

        var success = PaginationCursor.TryDecode(cursor, def, out var values, out var metadata);

        success.Should().BeTrue();
        metadata.ValueCount.Should().Be(1);
        metadata.Fingerprint.Should().Be(def.SchemaFingerprint);
    }

    [Fact]
    public void TryDecode_WithMismatchedFingerprint_ReturnsFalse()
    {
        var defV1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(defV1, new MainModel { Id = 10 });

        var defV2 = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
        PaginationCursor.TryDecode(cursor, defV2, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Fingerprint_SameDefinition_ProducesSameHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        def1.SchemaFingerprint.Should().Be(def2.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_DifferentColumns_ProducesDifferentHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created));
        def1.SchemaFingerprint.Should().NotBe(def2.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_DifferentDirection_ProducesDifferentHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Id));
        def1.SchemaFingerprint.Should().NotBe(def2.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_DifferentColumnCount_ProducesDifferentHash()
    {
        var def1 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var def2 = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id).Ascending(x => x.Created));
        def1.SchemaFingerprint.Should().NotBe(def2.SchemaFingerprint);
    }

    [Fact]
    public void TryDecode_CursorIssuedByAnotherProcess_Succeeds()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        PaginationCursor.TryDecode(IdCursor, def, out var values, out _).Should().BeTrue();
        PaginationCursor.Encode(def, values).Should().Be(IdCursor);
    }

    [Fact]
    public void TryDecode_NullableDescendingMultiColumnCursorIssuedByAnotherProcess_Succeeds()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.CreatedNullable).Ascending(x => x.Id));

        PaginationCursor.TryDecode(CreatedNullableDescendingThenIdCursor, def, out var values, out _).Should().BeTrue();
        PaginationCursor.Encode(def, values).Should().Be(CreatedNullableDescendingThenIdCursor);
    }

    [Fact]
    public void Fingerprint_ColumnTypeFromAnotherAssemblyVersion_IsUnchanged()
    {
        var first = FingerprintOfNullableEnumColumn(new Version(1, 0, 0, 0));
        var second = FingerprintOfNullableEnumColumn(new Version(2, 0, 0, 0));

        second.Should().Be(first);
    }

    // The same column type, as two releases of an application ship it.
    private static uint FingerprintOfNullableEnumColumn(Version version)
    {
        var module = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("FingerprintProbe") { Version = version }, AssemblyBuilderAccess.RunAndCollect)
            .DefineDynamicModule("FingerprintProbe");

        var status = module.DefineEnum("Probe.Status", TypeAttributes.Public, typeof(int));
        status.DefineLiteral("Active", 0);
        var columnType = typeof(Nullable<>).MakeGenericType(status.CreateType());

        var row = module.DefineType("Probe.Row", TypeAttributes.Public | TypeAttributes.Class);
        var field = row.DefineField("_status", columnType, FieldAttributes.Private);
        var getter = row.DefineMethod("get_Status", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, columnType, Type.EmptyTypes);
        var il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
        row.DefineProperty("Status", PropertyAttributes.None, columnType, null).SetGetMethod(getter);
        var rowType = row.CreateType();

        var definition = typeof(PaginationQuery)
            .GetMethod(nameof(PaginationQuery.Build), [typeof(string), typeof(bool), typeof(string), typeof(bool)])
            .MakeGenericMethod(rowType)
            .Invoke(null, ["Status", false, null, false]);
        return (uint)definition.GetType()
            .GetProperty(nameof(PaginationQueryDefinition<MainModel>.SchemaFingerprint), BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(definition);
    }
}
