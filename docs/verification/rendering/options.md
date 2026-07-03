## Options Unit Verification

Part of the Rendering Model Verification.

This document describes the verification design for the options unit of the
`DemaConsulting.Rendering` system. It maps every options unit requirement to at least one named test
scenario so a reviewer can confirm coverage without reading the test code.

### Options Unit Verification Approach

The Options unit is verified by direct in-process xUnit tests that construct a fresh
`PropertyHolder`, perform the operation under test, and assert on the observable result. No mocking
or stubbing is required: `LayoutProperty<T>` is a small immutable value type constructed directly in
each test, and `PropertyHolder` is the concrete store consumed as-is. There are no injected
dependencies to substitute because the unit has none beyond the .NET base class library. Each test
exercises one API on `IPropertyHolder` (`Get`, `TryGet`, `Set`, or `Contains`) and asserts that
either the stored value or the property's declared default is returned as specified by the design.

### Options Unit Test Environment

- **Framework**: xUnit v3, run through the standard `dotnet test` runner.
- **Test project**: `DemaConsulting.Rendering.Tests`, source file `PropertyHolderTests.cs`.
- **Runtime**: any target framework built by the solution (`net8.0`, `net9.0`, or `net10.0`).
- **Dependencies**: none beyond the standard test runner; no external services, network, filesystem,
  or configuration is required.
- **Isolation**: each test constructs its own `PropertyHolder` and `LayoutProperty<T>` instances, so
  there is no shared mutable state between tests and no ordering dependency.

### Options Unit Acceptance Criteria

Every named scenario listed below passes without error or unexpected exception (IEC 62304 §5.5.2). A
failure is any wrong stored value, wrong `TryGet`/`Contains` result, missing default, or unexpected
exception. The verification run is considered complete when every requirement listed in the
Requirements Coverage section is mapped to at least one passing test.

### Options Unit Scenarios

#### Unset property returns default

Test `Get_UnsetProperty_ReturnsDefault` reads a property from a fresh `PropertyHolder` without setting
it and asserts that the read returns the property's declared default value.

**Covers**: `Rendering-Model-Options-Default`.

#### Set value is retrieved

Test `Get_AfterSet_ReturnsStoredValue` sets a property to a value, then reads the same property and
asserts that the read returns the stored value rather than the default.

**Covers**: `Rendering-Model-Options-StoreAndRetrieve`.

#### Contains reflects explicit set

Test `Contains_ReflectsExplicitSet` queries `Contains` before and after setting a property and asserts
that it returns false before the set and true afterwards.

**Covers**: `Rendering-Model-Options-Contains`.

#### TryGet reports unset and yields default

Test `TryGet_UnsetProperty_ReturnsFalseAndDefault` calls `TryGet` for a property that has not been set
and asserts that it returns false and yields the declared default through its out parameter.

**Covers**: `Rendering-Model-Options-TryGet`.

### Requirements Coverage

- **`Rendering-Model-Options-Default`**: Get_UnsetProperty_ReturnsDefault
- **`Rendering-Model-Options-StoreAndRetrieve`**: Get_AfterSet_ReturnsStoredValue
- **`Rendering-Model-Options-Contains`**: Contains_ReflectsExplicitSet
- **`Rendering-Model-Options-TryGet`**: TryGet_UnsetProperty_ReturnsFalseAndDefault
