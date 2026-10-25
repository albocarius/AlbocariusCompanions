using System.Collections.Generic;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlbocariusCompanions.Core.Tests;

public class DebugCommandsTests
{
    private static VariableStore Store()
    {
        var dialogs = new DialogFile
        {
            Variables = new Dictionary<string, VariableDeclaration>
            {
                ["told_scar"] = new VariableDeclaration { Type = "bool", Scope = "shared" },
                ["raids"] = new VariableDeclaration { Type = "int", Default = new JValue(2) },
            },
        };
        return new VariableStore(new[] { new CompanionPackage("jeremus", "/x/jeremus", new CompanionDefinition(), dialogs) });
    }

    [Fact]
    public void List_shows_state_and_every_variable() =>
        Assert.Equal("jeremus: in party; mood=0, raids=2, told_scar=false", DebugCommands.List(Store(), _ => "in party"));

    [Fact]
    public void List_without_companions() =>
        Assert.Equal("No companions loaded.", DebugCommands.List(new VariableStore(), _ => ""));

    [Fact]
    public void Get_shows_value_type_scope_and_default() =>
        Assert.Equal("jeremus.raids = 2 (int, local, default 2)", DebugCommands.Get(Store(), new[] { "jeremus.raids" }));

    [Fact]
    public void Set_parses_by_type_and_writes()
    {
        var store = Store();
        Assert.Equal("jeremus.told_scar = true", DebugCommands.Set(store, new[] { "jeremus.told_scar", "true" }));
        Assert.Equal("jeremus.raids = -4", DebugCommands.Set(store, new[] { "jeremus.raids", "-4" }));
        Assert.Equal(1, store.Get("jeremus", "told_scar"));
        Assert.Equal(-4, store.Get("jeremus", "raids"));
    }

    [Theory]
    [InlineData("jeremus.told_scar", "3")]
    [InlineData("jeremus.raids", "true")]
    [InlineData("jeremus.nope", "1")]
    [InlineData("bora.x", "1")]
    [InlineData("told_scar", "true")]
    public void Set_rejects_bad_input_without_writing(string reference, string value)
    {
        var store = Store();
        Assert.StartsWith("Error: ", DebugCommands.Set(store, new[] { reference, value }));
        Assert.Empty(store.ToList());
    }

    [Fact]
    public void Wrong_argument_count_prints_usage()
    {
        Assert.Equal(DebugCommands.UsageGet, DebugCommands.Get(Store(), new string[0]));
        Assert.Equal(DebugCommands.UsageSet, DebugCommands.Set(Store(), new[] { "jeremus.raids" }));
        Assert.Equal(DebugCommands.UsageReset, DebugCommands.Reset(Store(), new[] { "a", "b" }));
    }

    [Fact]
    public void Reset_clears_written_values()
    {
        var store = Store();
        store.Set("jeremus", "raids", 9);
        Assert.Equal("jeremus: 1 written value(s) reset; every variable is at its default.", DebugCommands.Reset(store, new[] { "jeremus" }));
        Assert.Equal(2, store.Get("jeremus", "raids"));
        Assert.StartsWith("Error: ", DebugCommands.Reset(store, new[] { "bora" }));
    }
}
