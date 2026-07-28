# Changelog

All notable changes to VFX Mesh Generator are documented in this file.

## 0.4.0 - 2026-07-28

### Added

- Added independent, project-persistent shape settings so switching shape modes restores each shape's own parameters.
- Added an optional custom texture field for UV Checker preview mode, with the procedural checker retained as the fallback.

### Fixed

- Reversed Helix topology so its authored front faces point toward the positive Main Axis for either turn direction.
- Split Sphere and Hemisphere Shape Default UV poles per angular sector to remove pinwheel and zigzag interpolation artifacts.
- Preserved smooth pole normals and consistent packed vertex data across the new UV-only vertex splits.

## 0.3.1 - 2026-07-28

### Fixed

- Anchored Arc angular-width scaling to the outer rim so mirrored slash shells stay joined when the outer elevation is zero.
- Removed the Shaded Wireframe depth offset that could pull occluded edges through foreground polygons.

## 0.3.0 - 2026-07-28

### Added

- Added Project-window folder drag and drop for the default output folder.
- Added project-scoped persistence for the current recipe, output folder, preview preferences, and editor foldouts.
- Added an optional mirrored Arc shell with matching UVs for volumetric slash meshes.

### Fixed

- Fixed Radial projection at Disc center poles to prevent pinwheel-shaped UV distortion.

## 0.2.0 - 2026-07-27

### Added

- Made Arc a distinct partial-sweep shape with an angular width curve for tapered slash and crescent meshes.
- Added a combined Shaded Wireframe preview mode.
- Added contextual tooltips throughout the generator UI.

### Changed

- Made Ring a dedicated closed 360-degree shape.

## 0.1.1 - 2026-07-26

### Added

- Added curve-driven axial elevation for Ring and Arc meshes.
- Added opaque red backface visualization.

### Changed

- Removed the hard URP package-version dependency.
- Reversed vertical preview orbit dragging.

## 0.1.0 - 2026-07-26

### Added

- Added the initial editor-only procedural mesh generator.
- Added 14 base shapes, an ordered modifier stack, UV projection, VFX vertex data, preview modes, presets, and native Mesh asset output.
