# Changelog

All notable changes to VFX Mesh Lab are documented in this file.

## 1.0.0 - 2026-07-29

### Added

- Added seven production-authored built-in templates: Cross Plane, Droplet, Fan, Shockwave, Slash, Spiral, and Splash.
- Added an `Apply Built-In Template...` menu to load a template directly into the editor without locating package assets manually.
- Added installed-package tests that load and generate every built-in template.

### Changed

- Package presets are now treated as read-only in the editor. Use `Save New` to create an editable project copy inside `Assets`.
- Disc, Ring, and Arc now keep generator-authored radial V coordinates for Shape Default and Radial UV layouts while distribution curves and later modifiers move vertices, enabling stretched mappings and variable texture-scroll speed.

## 0.6.0 - 2026-07-28

### Added

- Added a Reset button that restores only the active shape profile while preserving every other shape and recipe setting.
- Added partial Disc sweeps for open fan meshes.
- Added axial elevation and radial vertex-distribution curves for Disc, with the distribution control also available on Ring and Arc.
- Exposed context-specific scale curves for Quad, Cone, Cylinder, Tube, Sphere, Hemisphere, Torus, Box, Cross Planes, Ribbon, and Helix.
- Extended width-aware Shape Default UV modes to Quad, Cross Planes, and Helix strips.

### Fixed

- Kept full Torus profile-scale seams closed when curve endpoints differ.
- Smoothed duplicated angular seam normals on elevated full Disc meshes.
- Preserved smooth curved-surface normals across UV-only center and seam splits.

## 0.5.0 - 2026-07-28

### Added

- Added Outer Rim, Middle, and Inner Rim origins for Arc angular-width scaling.
- Added Preserve Texel Density and Stretch To Width modes for Ribbon Shape Default UVs.

### Fixed

- Removed triangle-by-triangle checker kinks from tapered Ribbon Shape Default UVs by using an affine, width-aware layout by default.
- Kept mirrored Arc outer rims joined for every width origin, including elevation curves with a nonzero outer endpoint.

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
