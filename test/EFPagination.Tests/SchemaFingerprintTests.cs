using System.Globalization;
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
    private const string ComputedColumnCursor = "BQBq63eRAgAAAPi0yEjeCAEKAAAA";

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
    public void TryDecode_CursorOfAnotherComputedColumn_ReturnsFalse()
    {
        var byCreatedNullable = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.CreatedNullable ?? x.Created).Ascending(x => x.Id));
        var byCreatedPlusDay = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created.AddDays(1)).Ascending(x => x.Id));
        var cursor = PaginationCursor.Encode(byCreatedNullable, new MainModel { Id = 10, Created = new DateTime(2026, 1, 1) });

        PaginationCursor.TryDecode(cursor, byCreatedPlusDay, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Fingerprint_ComputedColumnWithAnotherParameterName_IsUnchanged()
    {
        var byX = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.CreatedNullable ?? x.Created));
        var byRow = PaginationQuery.Build<MainModel>(b => b.Ascending(row => row.CreatedNullable ?? row.Created));

        byRow.SchemaFingerprint.Should().Be(byX.SchemaFingerprint);
    }

    [Fact]
    public void Fingerprint_ComputedColumnConstantInAnotherCulture_IsUnchanged()
    {
        var invariant = FingerprintInCulture(CultureInfo.InvariantCulture);
        var german = FingerprintInCulture(CultureInfo.GetCultureInfo("de-DE"));

        german.Should().Be(invariant);

        static uint FingerprintInCulture(CultureInfo culture)
        {
            var current = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created.AddDays(1.5))).SchemaFingerprint;
            }
            finally
            {
                CultureInfo.CurrentCulture = current;
            }
        }
    }

    [Fact]
    public void Fingerprint_ComputedColumnCapturingALocalInAnotherMethod_IsUnchanged()
    {
        FingerprintCapturingDaysInSecondMethod().Should().Be(FingerprintCapturingDaysInFirstMethod());
    }

    [Fact]
    public void TryDecode_ComputedColumnCursorIssuedByAnotherProcess_Succeeds()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.CreatedNullable ?? x.Created).Ascending(x => x.Id));

        PaginationCursor.TryDecode(ComputedColumnCursor, def, out var values, out _).Should().BeTrue();
        PaginationCursor.Encode(def, values).Should().Be(ComputedColumnCursor);
    }

    [Fact]
    public void Fingerprint_ColumnTypeFromAnotherAssemblyVersion_IsUnchanged()
    {
        var first = FingerprintOfNullableEnumColumn(new Version(1, 0, 0, 0));
        var second = FingerprintOfNullableEnumColumn(new Version(2, 0, 0, 0));

        second.Should().Be(first);
    }

    // Each lambda captures its own local, so the compiler gives each method its own closure class.
    private static uint FingerprintCapturingDaysInFirstMethod()
    {
        var days = 1.5;
        return PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created.AddDays(days))).SchemaFingerprint;
    }

    private static uint FingerprintCapturingDaysInSecondMethod()
    {
        var days = 1.5;
        return PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created.AddDays(days))).SchemaFingerprint;
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
