# Changelog

## 0.3.1

- Anchored Arc angular-width scaling to the outer rim so mirrored slash shells stay joined when the outer elevation is zero.
- Removed the shaded-wireframe depth offset that could pull occluded edges through foreground polygons.

## 0.3.0

- Added Project-window folder drag and drop for the default output folder.
- Added project-scoped persistence for the current recipe, output folder, preview preferences, and editor foldouts.
- Fixed Radial projection at Disc center poles to prevent pinwheel-shaped UV distortion.
- Added an optional mirrored Arc shell with matching UVs for volumetric slash meshes.

## 0.2.0

- Made Ring a dedicated closed 360-degree shape and Arc a distinct partial-sweep shape.
- Added an angular width curve to Arc for tapered slash and crescent meshes.
- Added a combined Shaded Wireframe preview mode.
- Added contextual tooltips throughout the generator UI.

## 0.1.1

- Removed the hard URP package-version dependency.
- Added curve-driven Y elevation for Ring and Arc meshes.
- Reversed vertical preview orbit dragging.
- Added opaque red backface visualization.

## 0.1.0

- Initial implementation.
