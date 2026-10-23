# SteelValidationEngine

Deterministic, unit-tested rule checks for steel-structure model data: bolt edge distance and pitch, geometry sanity checks, numbering gaps, clash classification, drawing readiness and export verification.

> **Status: early / pre-release.** No tagged releases, no published packages, no known external users. APIs may change. Not a design-code check and not a fabrication release tool.

## Design

- The libraries target `netstandard2.0` and have **no third-party package dependencies** and no dependency on any CAD/BIM vendor assembly.
- Rules are pure functions over plain input objects. An adapter (not included here) is responsible for reading a model and filling those inputs.
- Fail closed: if evidence is missing, unverified, stale or ambiguous, the result is `Unknown` or "stale", never a pass.

## Projects

| Path | Purpose |
|------|---------|
| `src/SteelValidation.Abstractions` | Plain result/input contracts and `IModelAdapter` |
| `src/SteelValidation.Core` | Rules: `Bolts`, `Geometry`, `Clash`, `Numbering`, `Model`, `Drawings`, `Evidence`, `Revision`, `Reporting` |
| `tests/SteelValidation.Tests` | xUnit tests (no CAD software needed) |

## Build and test

Requires the .NET SDK (tests target `net10.0`).

```
dotnet test SteelValidationEngine.sln
```

## Bolt distance check: assumptions

All lengths are in **millimetres**. `d` is the **nominal bolt diameter**, never hole diameter or hole tolerance.

- Edge rule: distance from each bolt centre to the plate boundary must be `>= 2d`.
- Pitch rule: for each bolt, the distance to its nearest neighbour **in the same bolt group** must be `>= 3d`.
- 2d and 3d are the default screening rules of this engine, currently hard-coded. They are not a universal or design-code standard; real projects may require other values.
- Equality passes. A numeric tolerance of at most `1e-7 mm` (and at most `1e-9` of the required value) absorbs floating-point round-off only; it is not a manufacturing tolerance.
- Outcomes: `Pass`, `Fail`, `Unknown`, `NotApplicable`. Unknown evidence never passes. `AllChecksPassed` is true only if every finding passed.
- Scope is **within one bolt group and one target plate**. Distances between different groups are not checked.
- All centres of a group must be given in one declared Cartesian coordinate frame.
- Hole-only groups and groups without bolts are `NotApplicable`; incomplete or unverified positions are `Unknown`.

`PlanarBoltEdge.Measure` (edge distance helper) supports only:

- a straight-edged, convex, planar outer contour (at least 3 points, no self-intersection);
- a bolt axis exactly normal to the plate plane (skewed holes return `null`);
- contours the caller has already reduced to the true outer boundary (no chamfers, holes or cuts).

The bolt axis is intersected with the plate plane before measuring. A bounding box is never used. Anything unsupported or uncertain returns `null`.

## Limitations

- No model reader is included; you must supply the inputs.
- Report text produced by `Reporting` is currently in Turkish.
- Some doc comments refer to the model software the inputs originally came from.
- The compiler currently emits CS8618 (non-nullable property) warnings in `SteelValidation.Abstractions`.

## Trademarks

Product names mentioned in comments belong to their owners. This project is independent and not affiliated with or endorsed by them.

## License

Not yet decided. No license file has been added, so all rights are reserved until one is.