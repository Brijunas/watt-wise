---
paths:
  - '**/*.cs'
---

# Class design

- Every class is `sealed` unless it is an abstract base meant for derivation. Never `protected` members in a sealed class.

Why: "Sealed by default" in [conventions.md](../../docs/conventions.md#code-style).
