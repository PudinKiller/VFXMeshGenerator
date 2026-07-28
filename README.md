# VFX Mesh Generator

![Unity](https://img.shields.io/badge/Unity-6%2B-black)
![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-5b7fff)
![License](https://img.shields.io/badge/License-MIT-blue)
![Version](https://img.shields.io/badge/version-0.5.0-orange)

A free and open-source Unity 6+ editor tool for generating and art-directing procedural meshes for real-time VFX workflows.

Create slashes, impact rings, beams, ribbons, cards, spirals, and other VFX-ready meshes without leaving Unity. Build a base shape, apply a non-destructive modifier stack, configure UV and vertex data, inspect the result, and save it as a native Unity `Mesh` asset.

Made by [Ken Deng / PudinKiller](https://github.com/PudinKiller).

---

## Feature Demos

> [!NOTE]
> The demo areas below are placeholders. Replace them with your GIF links when the recordings are ready.

<table>
  <tr>
    <td width="50%" align="center">
      <b>GIF placeholder: Shape and Modifier Workflow</b>
      <br>
      <code>.github/readme/ShapeModifiers.gif</code>
      <br><br>
      <sub>Choose a VFX shape, adjust its resolution, and build a layered modifier stack.</sub>
    </td>
    <td width="50%" align="center">
      <b>GIF placeholder: Mirrored Arc Slash</b>
      <br>
      <code>.github/readme/MirroredArc.gif</code>
      <br><br>
      <sub>Shape a tapered Arc, add axial elevation, and mirror it into a volumetric slash shell.</sub>
    </td>
  </tr>
</table>

<details>
<summary><b>Full workflow demo — GIF placeholder</b></summary>

<br>

Add the full workflow GIF here after recording it.

Suggested path:

```text
.github/readme/Workflow.gif
```

Suggested alt text:

```text
VFX Mesh Generator full workflow demo
```

</details>

---

## Why This Exists

Real-time VFX often needs small, specialized meshes: a curved slash, an impact ring, a tapered ribbon, a hollow beam, crossed smoke cards, or a mesh carrying custom data for a shader.

These meshes are usually simple, but moving to Blender, Maya, Houdini, or another DCC for every small revision can interrupt iteration. The geometry may then need another pass for UV direction, pivots, vertex colors, additional data channels, winding, or double-sided output before it is ready for an effect.

**VFX Mesh Generator is not trying to replace a full modeling package.**

It is a focused Unity editor utility for the procedural VFX meshes that need to be created, adjusted, previewed, and regenerated quickly during production.

---

## Core Features

| Feature | What it is useful for |
|---|---|
| 14 procedural base shapes | Start from common VFX geometry instead of an empty modeling scene |
| Ordered modifier stack | Layer transform, taper, twist, bend, wave, ripple, noise, and other deformations |
| Curve-driven profiles | Art-direct Ribbon and Helix width, Ring elevation, and Arc width/elevation |
| Mirrored Arc shells | Build paired slash or crescent shells with matching UV direction |
| UV projection tools | Generate planar, radial, cylindrical, spherical, along-length, or box UV0 |
| VFX vertex data | Write colors and packed `Vector4` data into UV1, UV2, and UV3 |
| Live diagnostic preview | Inspect shading, UVs, normals, vertex colors, wireframe, and backfaces |
| Reusable recipe presets | Save, load, and update complete generator configurations |
| Native Mesh output | Create standalone `.asset` meshes with no runtime package dependency |
| Reference-preserving updates | Regenerate an existing Mesh asset while preserving its GUID and references |
| Project-scoped persistence | Restore independent settings per shape, output folder, preview preferences, and foldouts |
| Editor safety limits | Reject excessive topology before synchronous generation begins |

---

## Requirements

- Unity `6000.0` or newer
- A compatible Universal Render Pipeline package installed in the project
- Git, only when installing directly from a Git URL

> [!IMPORTANT]
> Install URP before VFX Mesh Generator. The preview shader uses URP shader libraries, but the package intentionally does not pin a specific URP package version.

The tool is editor-only. Generated Mesh assets are normal Unity assets and do not require VFX Mesh Generator at runtime.

---

## Installation

### Option 1: Install a Tagged Release from Git

This method requires Git to be installed on your computer.

In Unity:

1. Open `Window > Package Manager`.
2. Click the `+` button.
3. Choose `Add package from git URL`.
4. Paste:

```text
https://github.com/PudinKiller/VFXMeshGenerator.git#v0.5.0
```

Using a version tag keeps the installed package stable. To follow the latest code on `main`, omit `#v0.5.0`.

<details>
<summary><b>Install without Git</b></summary>

### Option 2: Install Using ZIP

Use this method if Unity says Git is not installed.

1. Open the [GitHub Releases page](https://github.com/PudinKiller/VFXMeshGenerator/releases).
2. Download `Source code (zip)` for the version you want.
3. Unzip it somewhere on your computer.
4. In Unity, open `Window > Package Manager`.
5. Click the `+` button.
6. Choose `Add package from disk`.
7. Select the unzipped package's `package.json`.

### Option 3: Install Using `.tgz`

If a `.tgz` package is attached to a GitHub Release:

1. Download the `.tgz` file.
2. In Unity, open `Window > Package Manager`.
3. Click the `+` button.
4. Choose `Install package from tarball`.
5. Select the downloaded `.tgz` file.

</details>

---

## Quick Start

Open the tool from:

```text
Tools > VFX Mesh Generator
```

Basic workflow:

1. Enter a Mesh Name.
2. Choose a base Shape.
3. Adjust its dimensions, resolution, Main Axis, and Pivot.
4. Add and reorder any modifiers.
5. Configure UV0 and optional VFX Vertex Data.
6. Inspect the live preview and topology statistics.
7. Choose Mesh Output settings.
8. Click `Generate New` to create a Mesh asset.

To regenerate an asset later, assign it to `Update Mesh` and click `Update Existing`.

> [!TIP]
> Save the complete setup as a recipe preset before experimenting with a major variation.

---

## Preview Controls

| Input | Action |
|---|---|
| Left mouse drag | Orbit |
| Right or middle mouse drag | Pan |
| Mouse wheel | Zoom |
| Double-click | Frame the generated mesh |
| `F` | Frame the generated mesh |
| `O` | Toggle orthographic preview |
| Front / Side / Top | Snap to an axis-aligned view |

Preview modes:

- `Shaded`
- `Unlit`
- `Wireframe`
- `UV Checker`
- `Normals`
- `Vertex Colors`
- `Shaded Wireframe`

Backfaces are drawn in red to make winding and open geometry problems easier to identify. Preview visualization does not change the saved mesh.

When `UV Checker` is selected, the toolbar exposes an optional custom Texture2D. Leave it empty for the built-in procedural checker; assigned textures use their import filtering and wrap settings.

The overlay displays vertex count, triangle count, bounds, validation flags, and generation warnings.

---

## Common Workflows

<details>
<summary><b>Create a tapered mirrored slash</b></summary>

Start with:

```text
Shape: Arc
Inner Radius: Set the inside edge
Outer Radius: Set the outside edge
Arc Degrees: Set the slash sweep
Angular Width Curve: Taper one or both ends
Radial Width Origin: Anchor the taper at the outer rim, middle, or inner rim
Mirror Across Shape Plane: On
```

Useful follow-up settings:

```text
Axial Elevation Curve: Shape the shell profile
UV Projection: Along Length or Shape Default
Double Sided: Enable only when independently lit backfaces are required
```

The Angular Width Curve scales radial thickness around the selected origin. `Outer Rim` moves only the inner radius, `Middle` moves both radii evenly, and `Inner Rim` moves only the outer radius. A zero curve value collapses the strip to the selected radial origin. Axial elevation remains referenced to the outer rim so mirrored shells stay joined.

`Mirror Across Shape Plane` creates a disconnected reflected shell with matching UVs and aligns both shells at the outer rim. It is different from `Double Sided`, which duplicates reversed faces at the same positions.

</details>

<details>
<summary><b>Create an impact or shockwave ring</b></summary>

Start with:

```text
Shape: Ring
Inner Radius: Set the hole size
Outer Radius: Set the effect radius
Axial Elevation Curve: Shape a flat, raised, or curved profile
```

Try a radial or shape-default UV layout depending on how the shader samples its texture.

Use vertex colors or a packed UV channel to store normalized radial distance for masks, erosion, displacement, or timing offsets.

</details>

<details>
<summary><b>Create a tapered ribbon or trail mesh</b></summary>

Start with:

```text
Shape: Ribbon
Width Curve: Taper the start and end
Length Segments: Increase before adding deformation
UV Projection: Shape Default
Shape Default Width UV: Preserve Texel Density
```

Useful modifiers:

```text
Bend -> Wave -> Noise
```

Modifier order matters. A wave added after a bend produces a different result from bending an already-wavy ribbon.

`Preserve Texel Density` narrows the UV footprint with the ribbon and removes diagonal interpolation kinks. Use `Stretch To Width` when every row must fill U 0-1; ordinary triangle interpolation can show chevrons on low-resolution tapers in that mode.

</details>

<details>
<summary><b>Create a beam, tunnel, or hollow volume</b></summary>

Use:

```text
Cylinder: Filled cylindrical body
Tube: Hollow cylindrical body
Cone: Tapered or pointed volume
```

Set `Arc Degrees` below 360 for an open radial section. Use `Cap Start` and `Cap End` to close the minimum and maximum ends along the Main Axis.

Useful modifiers include Taper, Twist, Wave, Noise, and Inflate.

</details>

<details>
<summary><b>Create crossed smoke, flame, or foliage cards</b></summary>

Start with:

```text
Shape: Cross Planes
Plane Count: 2 or more
Main Axis: Match the effect's vertical direction
```

Use an Axis Gradient in Vertex Colors to store a bottom-to-top mask. Add extra intersecting planes when the effect needs more angular coverage.

</details>

<details>
<summary><b>Create an energy spiral</b></summary>

Start with:

```text
Shape: Helix
Turns: Number of revolutions
Pitch: Distance traveled per revolution
Strip Width: Base ribbon width
Width Curve: Taper along the spiral
```

Add Twist, Wave, or Noise for secondary motion. Use `Along Length` UVs for scrolling textures.

</details>

---

## Shape Reference

| Shape | Main controls | Typical VFX uses |
|---|---|---|
| Quad | Width, length, subdivisions | Sprites, decals, flashes, simple cards |
| Disc | Radius, edge count, radial resolution | Circular flashes, ground effects, radial masks |
| Ring | Inner/outer radius, radial resolution, elevation curve | Shockwaves, portals, ground rings |
| Arc | Ring controls, sweep, width curve origin, elevation, mirrored shell | Slashes, crescents, directional shockwaves |
| Cone | Height, bottom/top radius, radial and height segments, caps | Spot volumes, directional bursts, funnels |
| Cylinder | Height, radius, radial and height segments, caps | Beams, columns, volumes |
| Tube | Height, inner/outer radius, radial and height segments, caps | Hollow beams, tunnels, cylindrical shells |
| Sphere | Radius, longitude, latitude | Energy fields, bursts, spherical masks |
| Hemisphere | Radius, longitude, latitude, equator cap | Domes, ground shields, explosion shells |
| Torus | Major/minor radius, ring/tube segments, sweep | Portals, energy loops, curved bands |
| Box | Size and X/Y/Z subdivisions | Volumes, distortion regions, box masks |
| Ribbon | Width, length, width curve, Shape Default width UV mode, subdivisions | Trails, streaks, tapered strips |
| Cross Planes | Width, height, plane count, subdivisions | Smoke, flame, foliage, volumetric cards |
| Helix | Radius, strip width, turns, pitch, width curve | Spirals, coils, energy trails |

Every shape also supports:

- Main Axis: `X`, `Y`, or `Z`
- Pivot: `Center`, `Start`, `End`, or `Custom`
- Resolution controls appropriate to its topology

`Ring` is always a closed 360-degree loop. Use `Arc` when you need a partial sweep or an angular width profile.

---

## Modifier Reference

Modifiers are evaluated from top to bottom and can be enabled, bypassed, reordered, or removed.

| Modifier | What it does |
|---|---|
| Transform | Offsets, scales, and rotates the complete mesh |
| Taper | Changes radial scale according to falloff |
| Twist | Rotates vertices progressively around an axis |
| Bend | Curves the mesh around the selected axis |
| Skew | Offsets the mesh progressively in axis or radial space |
| Wave | Adds repeating directional displacement |
| Radial Ripple | Adds wave displacement around the mesh center |
| Noise | Applies deterministic layered spatial displacement |
| Inflate | Pushes or pulls vertices using their normals |
| Spherize | Blends the mesh toward a spherical form |
| Flatten | Blends toward an axis plane or selected axis line |

Most deformation modifiers provide:

- An independent Axis
- Axis or Radial evaluation space
- An artist-authored falloff curve
- Strength, angle, phase, or frequency controls appropriate to the modifier

Noise also provides Seed, Octaves, Lacunarity, Persistence, sampling offset, and Axis or Radial displacement direction.

---

## UV0

UV0 is generated after deformation unless `Shape Default` is selected.

Available projections:

| Projection | Typical use |
|---|---|
| Shape Default | Preserve UVs authored by the selected shape generator |
| Planar | Flat cards, decals, and axis-aligned meshes |
| Radial | Discs, rings, shockwaves, and radial masks |
| Cylindrical | Beams, tubes, cones, and cylindrical effects |
| Spherical | Spheres, domes, and radial volumes |
| Along Length | Ribbons, trails, arcs, and helices |
| Box | Boxes and meshes that need three-axis projection |

Final UV transforms include:

- Scale
- Offset
- Rotation around `(0.5, 0.5)`
- Flip U
- Flip V
- Swap U/V

Use `UV Checker` preview mode to inspect orientation, scale, distortion, and seams before saving.

For tapered Ribbons, `Preserve Texel Density` is the artifact-free Shape Default layout: U remains proportional to local mesh width, so both triangles share one affine mapping. `Stretch To Width` keeps U at 0-1 on every row, but a tapered trapezoid cannot express that projective mapping exactly with ordinary interpolated triangle UVs. Increase subdivisions if that legacy layout is required.

Sphere and Hemisphere Shape Default UVs split the shared pole per longitude sector, preventing unrelated sectors from interpolating toward one pole U coordinate. Latitude-longitude UVs still compress naturally at the pole; more subdivisions or another projection can trade that compression for different seams.

---

## VFX Vertex Data

### Vertex Colors

Write one `COLOR` value per vertex using:

- `Solid`
- `Axis Gradient`
- `Radial Gradient`

The gradient axis can be selected independently, making it useful for dissolve masks, bottom-to-top fades, radial timing, or shader-driven color variation.

### Packed UV Channels

UV1, UV2, and UV3 can each store a `Vector4` shader stream:

| Unity channel | Shader semantic |
|---|---|
| UV1 | `TEXCOORD1` |
| UV2 | `TEXCOORD2` |
| UV3 | `TEXCOORD3` |

Each X/Y/Z/W component can use one of these sources:

- Zero
- One
- Normalized X
- Normalized Y
- Normalized Z
- Along Axis
- Radial Distance
- Deterministic Random

Position and distance sources are normalized to `0-1` using the final mesh bounds. `Along Axis` follows the shape's Main Axis, while `Radial Distance` measures perpendicular distance from that axis.

These channels can carry masks, timing offsets, variation, normalized positions, custom coordinates, or other shader data without requiring a custom mesh post-process.

---

## Mesh Output

| Setting | Effect |
|---|---|
| Flip Winding | Reverses every triangle's front face |
| Double Sided | Duplicates vertices and reversed triangles for independently lit backfaces |
| Flat Shading | Splits vertices per triangle to create hard face normals |
| Generate Tangents | Calculates tangents from UV0 for tangent-space effects |
| Read/Write Enabled | Keeps or discards the CPU-readable copy after saving |
| Optimize Mesh | Reorders saved vertex and index buffers |
| Compression | Applies Unity mesh compression to the saved asset |
| Index Format | Selects Auto, UInt16, or UInt32 indices |
| Bounds Padding | Expands calculated local bounds on every side |

`Double Sided` approximately doubles mesh size. `Flat Shading` can increase vertex count substantially because vertices are split per triangle.

`Auto` index format uses 16-bit indices when possible and switches to 32-bit above 65,535 vertices.

---

## Generating and Updating Assets

### Generate New

`Generate New` creates a standalone native `.asset` Mesh inside the project's `Assets` folder. If the selected name already exists, Unity generates a unique path rather than replacing it.

### Update Existing

`Update Existing` replaces the selected standalone Mesh asset's data while preserving its GUID and project references.

The update is registered with Unity Undo, and the writer keeps a temporary in-memory backup for rollback if saving fails.

> [!WARNING]
> Updating still changes the selected asset on disk. Keep the project under version control and confirm the `Update Mesh` field before clicking `Update Existing`.

Imported model sub-assets are not supported as update targets.

---

## Presets and Persistent State

A `VFX Mesh Recipe Preset` stores the complete generator setup:

- Shape and shape settings
- Ordered modifier stack
- UV0 settings
- Vertex colors and packed UV channels
- Mesh output settings

Preset controls:

- `Load`: replace the current recipe with the selected preset
- `Update`: overwrite the selected preset with the current recipe
- `Save New`: create a new preset asset

The editor also restores project-scoped working state, including independent parameters for each shape mode, the current recipe, output folder, preview mode, custom checker texture, preview background, modifier selection, foldouts, and selected preset.

---

## Safety Limits

Generation runs synchronously in the Unity editor, so the package validates topology before building.

Current limits include:

- 4,096 segments per axis
- 64 Cross Planes
- 12 Noise octaves
- 256 Helix turns
- Approximately 500,000 base vertices
- Approximately 1,000,000 final vertices after topology expansion

If a recipe exceeds a limit, the preview reports an error instead of attempting the build.

---

## Troubleshooting

<details>
<summary><b>The preview shader is missing or unsupported</b></summary>

Confirm that:

1. The project uses Unity 6 or newer.
2. A compatible Universal Render Pipeline package is installed.
3. The project has finished importing and compiling shaders.

The generator code is editor-only, but its preview shader includes URP shader libraries.

</details>

<details>
<summary><b>Unity says Git is not installed</b></summary>

The Git URL install method requires Git.

Use the ZIP or `.tgz` installation method instead.

</details>

<details>
<summary><b>The mesh appears invisible or inside out</b></summary>

Switch to a shaded preview and look for red backfaces.

Try:

```text
Flip Winding: On
```

or, when both sides must render as independent faces:

```text
Double Sided: On
```

</details>

<details>
<summary><b>The texture scrolls in the wrong direction</b></summary>

Try the UV transforms:

```text
Flip U
Flip V
Swap U/V
Rotation
```

For ribbons, arcs, and helices, compare `Shape Default` and `Along Length`.

</details>

<details>
<summary><b>My shader cannot read UV1, UV2, or UV3 data</b></summary>

Confirm that the channel is enabled in `VFX Vertex Data` and that the shader reads the matching semantic:

```text
UV1 -> TEXCOORD1
UV2 -> TEXCOORD2
UV3 -> TEXCOORD3
```

</details>

<details>
<summary><b>I cannot update a mesh from an FBX or another imported model</b></summary>

`Update Existing` supports standalone `.asset` Mesh objects only.

Generate a new standalone Mesh asset instead of selecting an imported model sub-asset.

</details>

<details>
<summary><b>UInt16 output fails</b></summary>

UInt16 supports at most 65,535 addressable vertices.

Reduce resolution or set:

```text
Index Format: Auto
```

or:

```text
Index Format: UInt32
```

</details>

<details>
<summary><b>The recipe exceeds the editor safety limit</b></summary>

Reduce shape resolution, plane count, or Helix turns.

Also consider disabling:

```text
Flat Shading
Double Sided
```

Both options expand topology after the base shape is generated.

</details>

---

## Limitations

- Unity 6+ and URP are currently required.
- The package is an editor authoring tool, not a runtime procedural mesh system.
- Generation is CPU-based and synchronous.
- Output is a native Unity `.asset` Mesh; FBX, OBJ, and other model export formats are not included.
- Imported model sub-assets cannot be updated in place.
- The tool does not provide manual vertex, edge, face, boolean, skinning, or general-purpose UV-unwrapping workflows.
- Mirrored Arc output uses two disconnected shells with matching UVs.
- High-resolution output can become expensive, especially with Flat Shading and Double Sided enabled.

---

## Roadmap

Possible future improvements:

- Example recipe presets
- Sample shaders and VFX Graph workflows
- More VFX-specific base shapes and deformation controls
- Recipe import and export
- Scene-view handles
- Additional vertex-data sources
- More documentation and production examples
- OpenUPM distribution

---

## Contributing

Bug reports, feature ideas, workflow suggestions, and pull requests are welcome.

When reporting a problem, please include:

- Unity version
- URP package version
- Operating system
- Installation method
- Shape and important recipe settings
- Reproduction steps
- Screenshot, recording, or preset when possible

Use the [GitHub issue tracker](https://github.com/PudinKiller/VFXMeshGenerator/issues) for public reports and requests.

---

## Development

<details>
<summary><b>Development Information</b></summary>

This repository is structured as a Unity Package Manager package.

```text
VFXMeshGenerator/
  package.json
  README.md
  CHANGELOG.md
  LICENSE.md
  Editor/
    Core/
    Generation/
    IO/
    Presets/
    Preview/
    Processing/
    Shaders/
    UI/
  Tests/
    Editor/
```

To test a local checkout:

1. Open `Window > Package Manager`.
2. Click the `+` button.
3. Choose `Add package from disk`.
4. Select this repository's `package.json`.

Run the package's Edit Mode tests from Unity's Test Runner.

</details>

---

## More Open-Source VFX Tools

[VFX Texture Lab](https://github.com/PudinKiller/VFXTextureLab) is a companion Unity editor tool for contrast, gradients, masks, channel packing, and other common VFX texture operations.

---

## License

MIT License.

You can use this tool in personal, educational, and commercial projects. See [LICENSE.md](LICENSE.md) for the complete license text.
