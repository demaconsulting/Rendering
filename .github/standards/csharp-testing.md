---
name: C# Testing
description: Follow these standards when developing C# tests.
globs: ["**/test/**/*.cs", "**/tests/**/*.cs", "**/*Tests.cs", "**/*Test.cs"]
---

# Required Standards

Read these standards first before applying this standard:

- **`testing-principles.md`** - Universal testing principles and dependency boundaries
- **`csharp-language.md`** - C# language development standards

# Package Reference

Every xUnit v4 test project requires the following package references for
`dotnet test` to discover and execute tests via Microsoft.Testing.Platform (MTP):

| Package | Purpose |
| ------- | ------- |
| `xunit.v3` | xUnit v4 framework (monolithic - includes assertions and fixtures; has built-in MTP support) |
| `Microsoft.Testing.Extensions.CodeCoverage` | MTP-native code coverage collector |
| `Microsoft.Testing.Extensions.TrxReport` | Adds the `--report-trx` option to produce TRX result files |

Also set `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`
in the project so the compiled test assembly runs as a standalone MTP executable.
`Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` (the VSTest host/adapter)
are no longer required and should not be referenced.

If tests require mocking of dependencies, add `NSubstitute` as a package
reference - it is recommended when mocking is needed but is not required for
every test project.

# Test Style

Test names appear in requirements traceability matrices - use the hierarchical
naming pattern, and follow AAA with labeled comments:

- **System tests**: `{SystemName}_{Functionality}_{Scenario}_{ExpectedBehavior}`
- **Subsystem tests**: `{SubsystemName}_{Functionality}_{Scenario}_{ExpectedBehavior}`
- **Unit tests**: `{ClassName}_{MethodUnderTest}_{Scenario}_{ExpectedBehavior}`

```csharp
/// <summary>
///     Validates that an invalid email format throws an ArgumentException.
/// </summary>
[Fact]
public void UserValidator_ValidateEmail_InvalidFormat_ThrowsArgumentException()
{
    // Arrange: create a validator with default configuration
    var validator = new UserValidator();

    // Act / Assert: email with no domain throws
    Assert.Throws<ArgumentException>(() => validator.ValidateEmail("not-an-email"));
}
```

# xUnit v4 Specifics

These are non-obvious v3/v4 behaviors that differ from v2 or common assumptions:

- **`IAsyncLifetime`**: Both `InitializeAsync` and `DisposeAsync` return `ValueTask`
  in v3, not `Task` - using `Task` compiles but does not satisfy the v3 interface
- **`Assert.Multiple`**: Use to collect all assertion failures in a single test
  rather than stopping at the first
- **`[Collection]` without `[CollectionDefinition]`**: Silently disables parallelism
  without providing any shared fixture - always pair them or remove `[Collection]`

# Quality Checks

- [ ] All tests follow AAA pattern with clear section comments
- [ ] Test names follow hierarchical naming pattern above
- [ ] Each test verifies single, specific behavior (no shared state between tests)
- [ ] Both success and failure scenarios covered including edge cases
- [ ] External dependencies mocked with NSubstitute (when mocking is needed)
- [ ] Tests linked to requirements with source filters where needed
- [ ] Test results generated in TRX format for ReqStream compatibility (`dotnet test --report-trx`)
