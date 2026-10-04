---
paths:
  - "src/**"
---

# General Conventions (all source)

Code conventions for any change under `src/`, applied when authoring and when reviewing. Also apply `csharp` for C#
code and `extensions` for code that uses `Microsoft.Extensions.*`. Where a more specific file conflicts with a general
one, the more specific file wins.

## Change Scope & Justification

- **Prefer the simplest solution that works.** The burden of proof is on the more complex approach. Unnecessary
  abstraction, extra indirection, and elaborate solutions for marginal gains are a cost, not a feature.
- **Use what the libraries ship before writing code.** Before designing a mechanism, check whether a library the package
  uses already provides it (EF Core, ASP.NET Core, the .NET runtime and `Microsoft.Extensions.*`, Roslyn for the
  analyzers), including its default and whether the code overrides that default. Write custom code only where none
  exists.
- **Fit the contract, not the nearest example.** Choose each type, member and pattern for the contract the code serves
  and for these rules, with evidence from source. That a library or this code already does something is not a reason on
  its own: that an expression runs on SQLite is no evidence that SQL Server translates it the same way, and a rule or an
  existing shape is checked against the code before it is followed.
- **Justify each addition.** New code, APIs, abstractions, and flags create a permanent maintenance obligation. If an
  addition can be avoided without sacrificing correctness or meaningful capability, avoid it.
- **Fix root cause, not symptoms or workarounds.** Investigate and fix the root cause rather than adding workarounds or
  suppressing warnings.
- **Don't bundle unrelated changes.** Keep each change to a single concern: no drive-by refactoring, no whitespace
  noise, no build artifacts. Large refactorings and mechanical renames belong in their own change, separate from logic
  changes.

## Consistency with Codebase Patterns

### Code Reuse & Deduplication

- **Extract duplicated logic into shared helper methods.** Fix improvements inside shared helpers so all callers
  benefit.
- **Use existing APIs instead of creating parallel ones.** Before introducing new types, enums, or helpers, check if
  existing ones serve the same purpose. Fix existing utilities rather than introducing duplicates.
- **An entry point holds its own flow.** A step of one flow stays in the member that runs it: a private member that
  only holds part of its single caller's flow (a loop body, a retry around the next call) and passes its arguments
  down is a hop with no gain. Split code into its own member when it runs more than once (each item, each worker),
  when a library requires the shape, or for a technical reason stated in a comment, as aspnetcore marks
  `// Internal for testing` and `// Forcibly yield - we want to unblock the timer thread` in
  `HealthCheckPublisherHostedService`.
- **Delete dead code and unused declarations aggressively.** Remove dead code, unnecessary wrappers, obsolete fields,
  and unused variables when encountered or when the only caller changes. Also remove helper methods, enum values, and
  declarations left unused by a removal.

### Established Conventions

- **Preserve existing alphabetical ordering in modified lists.** When a change adds or reorders entries in an
  alphabetized list, verify that the changed entries preserve the surrounding order. Flag only ordering regressions
  introduced by the change; do not require unrelated cleanup of pre-existing unsorted entries.
- **Don't modify generated files manually.** Change the generator or source definition instead.
- **Match the surrounding code where the rules are silent.** Follow a file's naming, idiom and comment density when no
  rule decides the question. Where existing code breaks a rule, the rule wins: code that exists is not a reason to keep
  a pattern, and a change that touches it brings it in line.

## Documentation & Comments

- **Comments should explain why, not restate code.** Delete comments that just duplicate the code in English. Don't
  include historical context about why code changed.
- **Delete or update obsolete comments when corresponding code changes.** Stale comments describing old behavior are
  worse than no comments. Update them when you touch the relevant code; leave unrelated stale comments to a dedicated
  cleanup pass.
- **Track deferred work with GitHub issues and searchable TODOs.** Reference the tracking issue in TODO comments. Remove
  TODOs that will never be addressed.
- **Don't duplicate comments on interface implementations.** Documentation comments belong on the interface
  definition. Implementations should use `<inheritdoc/>` to avoid divergence.
- **Document every public API** as `docs.md` says. Test code follows the documentation rules of `AGENTS.md`, "Tests".
- **Cite code by file and symbol, never by line number.** Line numbers go stale. Code comments and XML docs name the
  file or symbol and never link to source (`docs.md`); a link to another repository in an issue, a PR or `AGENTS.md`
  uses a tag or commit SHA, not a branch.
- **Reference specs and authoritative sources in implementation code.** Cite the RFC or specification section the code
  implements.
- **Use established terminology in user-facing text.** Do not expose internal type names or private field names in
  error messages or responses.
