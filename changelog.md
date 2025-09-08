# HoyoToon 0.0.7

## Scripts:

- **MaterialDetection.cs**:
  - Added high-level method `DetectGameAutoOnly` to detect only the game key and return the source JSON path, useful for context scans.
  - Streamlined detection methods by introducing high-level auto methods while retaining low-level Try\* methods for explicit control.
  - Added comments to clarify the purpose of context helpers and recommended usage of `DetectFromContextPathWithSource` to retain provenance.
