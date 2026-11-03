# Changelog

## 1.1.0

- Add independent JCommonUtils.Core: deterministic hierarchy search, world horizontal sectors, and prefab pool lifecycle policies.
- Add Editor-only performance capture, bounded samples, statistics, and standalone JSON/HTML reports with explicit unavailable counters.
- Add executable contract/lifecycle tests and optional importable samples; no new editor menus.
- Keep legacy public event/singleton APIs and move the existing event dispatch contract tests into the package.
- Set the supported baseline to Unity 2022.3 and explicitly declare legacy UGUI/Newtonsoft/image conversion dependencies.

## 1.0.1 fork performance patch

- Replace event reflection dispatch with typed invocation while preserving reverse live traversal, reentrancy and exception wrapping.
