## Options Unit Verification

Part of the Rendering Model Verification.

This document describes the verification design for the options unit of the
`DemaConsulting.Rendering` system. It maps every options unit requirement to at least one named test
scenario so a reviewer can confirm coverage without reading the test code. The verification strategy,
test environment, and acceptance criteria are described in the
system verification document; the test project is `DemaConsulting.Rendering.Tests`
(`PropertyHolderTests.cs`).

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
