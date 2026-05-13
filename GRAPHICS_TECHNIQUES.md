# Graphics Techniques & Interview Prep — VR Network Visualization

---

## Project Introduction (speak this at interviews)

This project is an **immersive VR visualization platform** for exploring large-scale graph networks in real time, built entirely in Unity with a custom rendering pipeline targeting the Meta Quest headset.

The core problem it solves is visual scalability: graph networks can have thousands of nodes and tens of thousands of edges. Naively rendering each edge as a separate object would collapse frame rate immediately. So the entire system was designed around a GPU-first philosophy — the CPU prepares structured data, and the GPU does as much work as possible in parallel.

**What it does:**
- Renders networks as interactive 3D environments: nodes are lit spheres in 3D space, edges are smooth curved ribbons
- Edge bundling algorithm groups edges that travel similar routes, reducing visual clutter — the same way highway interchanges bundle traffic
- A terrain layer translates community structure into a landscape: dense communities become hills, you can literally "walk into" a cluster of related nodes
- A wrist-mounted minimap gives a bird's-eye overview without breaking VR immersion

**Key technical highlights I built:**
- A **GPU compute shader** that evaluates thousands of B-spline curves in parallel, feeding output directly to a custom HLSL shader that draws all edges in a **single draw call** — no CPU bottleneck regardless of edge count
- **Procedural geometry generation** in the vertex shader: ribbon quad edges are constructed entirely on the GPU, no mesh CPU upload needed
- **Custom instanced shaders** with per-node color so thousands of sphere nodes are drawn in one batched pass
- **Terrain rendering pipeline** with CPU-generated normal maps, procedural Delaunay triangulation, and a multi-texture compositing shader

This maps directly to what I'd do at Nuro: building performant visualization frameworks where the underlying data — whether it's a social graph or a sensor point cloud — is too large for naive per-object rendering, and where real-time performance is non-negotiable.

---

## 1. Render Pipeline

**Framework**: Unity Universal Render Pipeline (URP)

All shaders target URP with dual SubShader fallbacks:

| SubShader LOD | Mode | Use case |
|---|---|---|
| 200 | URP ForwardLit + shadow passes | Main rendering |
| 100 | Unlit fallback | Editor / legacy fallback |

**Frame rendering order:**
1. `BundledNetworkRenderer.UpdateRenderElements()` — syncs node/link CPU data
2. `BSplineShaderWrapper.Draw()` — dispatches compute shader → `Graphics.DrawProcedural` for all edges
3. Standard URP pass — GameObjects (nodes, terrain, UI panels)
4. `OverviewRenderer.Draw()` — wrist-mounted minimap particle update

**Why URP over Built-in RP:**
URP gives explicit control over each render pass, predictable performance on mobile VR hardware (Quest runs on a mobile SoC), and supports the compute shader + structured buffer pipeline needed for edge rendering. The built-in pipeline's fixed-function shadows and opaque batching don't give enough control for procedural draw calls.

**Forward vs. Deferred:**
The project primarily uses forward rendering. Each node is shaded once per light pass. Deferred rendering (GBuffer pass in `BasicLit.shader`) is included for non-network objects but not used for the thousands of network nodes, because deferred has high per-pixel memory cost for a scene with thin ribbon geometry.

---

## 2. Custom Shaders

### `NodeColorInstanced.shader`
Renders network nodes as lit spheres.

- **Lighting model**: Blinn-Phong (ambient + diffuse + specular)
- **Instancing**: `#pragma multi_compile_instancing` with `UNITY_INSTANCING_BUFFER_START(Props)` stores per-instance `_Color`
- **Shadows**: samples `_MainLightShadowmapTexture` via `SAMPLE_SHADOW_MAP`
- **Material feel**: metallic = 0.0, glossiness = 0.82 (plastic-specular look)
- **Transparency variant** (`CommColorInstanced.shader`): `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off` for community hull rendering

**Why Blinn-Phong instead of PBR:**
Nodes are abstract data objects, not physical materials. Blinn-Phong is faster (one specular lobe vs. full microfacet BRDF) and gives enough visual quality. PBR adds cost and realism that isn't meaningful here. For actual physical objects in the scene (environment props), `BasicLit.shader` uses URP's full PBR.

**Why ZWrite Off for community hulls:**
Community hulls are transparent enclosures around node groups. If ZWrite is on, a transparent surface writes its depth to the depth buffer, which causes geometry behind it to fail depth tests and disappear — you'd see nodes vanishing as you rotate around a community. Disabling ZWrite lets transparent and opaque geometry coexist correctly, at the cost of requiring correct back-to-front sorting.

### `BatchBSplineUnlit.shader`
Renders all network edges in a single draw call as view-aligned ribbon quads.

- **Input**: three `StructuredBuffer<SplineSamplePointData>` fed directly from the compute shader output
- **Ribbon geometry** (built in the vertex shader):
  - Each sample point is replicated 6× (2 triangles = 1 quad)
  - Tangent computed from adjacent sample positions
  - Quad normal = `normalize(cross(tangent, cameraDir))` → always faces camera
  - Width scales with distance: `_LineWidth / cameraDistance`
- **Color**: linearly interpolated from `startColor → endColor` stored per sample
- **Discard**: `clip(col.a - 0.001)` drops fully transparent fragments

**Why procedural ribbon quads instead of line primitives:**
OpenGL/DirectX line primitives have fixed 1px width and no perspective scaling. For VR at arm's length, thin lines disappear. Ribbon quads allow variable width, view-alignment, and proper perspective scaling. The vertex shader replicates each point 6× and computes offsets — no CPU mesh upload, the geometry is fully generated on GPU.

**Why a single draw call for all edges:**
At 10,000+ edges each sampled 10 times, that's 100,000+ render objects. Even at 0.1ms per draw call CPU overhead, that's 10 seconds per frame. `Graphics.DrawProcedural` submits one draw call with a vertex count of `segments × 10 × 6`, letting the GPU process everything in one submission. The vertex shader uses `SV_VertexID` to index into the structured buffer.

### `TerrainSurface.shader`
Composites three dynamically generated textures onto the terrain mesh.

- Reads `_LineTex` (edge lines), `_NodeColTex` (node color areas), `_SelectionTex` (selection highlights)
- **Screen blend formula**: `result = 1 − (1 − nodeColAndBg) × (1 − linkLineCol)` (additive-like, preserves brightness)
- **Contour lines** (optional): `fmod(height, step) < threshold` → discrete stepped coloring
- **Spherical curvature**: `h = length(ray) − _CurvatureRadius` adds subtle planetary curvature to flat terrain

**Why screen blending:**
Screen blend is like two light sources layering on a surface — neither washes the other out. A standard alpha blend would darken regions where both textures contribute. Screen blend preserves edge line brightness on top of colored node regions, which is exactly what you want for readability.

### `Minimap.shader`
Wrist-mounted overview; darkens regions outside the headset's FOV cone.

- Computes `dot(viewportDir, forwardDir)` and compares against `cos(_FOVAngle / 2)`
- Fragments outside the cone are multiplied by a darkening factor

**Why cosine comparison:** `dot(a, b) = |a||b|cos(θ)` for unit vectors. Comparing the dot product against `cos(halfAngle)` is equivalent to checking if the angle between the two vectors is less than `halfAngle`. This is a single MAD (multiply-add) per fragment — much cheaper than computing `acos` and comparing angles directly.

### `BasicLit.shader`
General-purpose URP surface shader used for non-network objects.

- Full URP pass suite: `ForwardLit`, `ShadowCaster`, `GBuffer` (deferred path), `DepthOnly`, `DepthNormals`, `Meta` (lightmapping)
- DOTS instancing support included

### `ParticleNode.shader`
Minimal unlit + vertex-color + fog shader for particle-based node representation in the overview.

---

## 3. Compute Shader — `BSpline.compute`

Evaluates cubic B-spline curves on the GPU, one thread per spline segment.

```
numthreads(32, 1, 1)   // 32 segments batched per group
```

**B-spline basis matrix** (uniform cubic, scaled 1/6):

```
M = (1/6) × [[-1,  3, -3,  1],
              [ 3, -6,  3,  0],
              [-3,  0,  3,  0],
              [ 1,  4,  1,  0]]
```

**Per-segment evaluation** (10 samples, t ∈ [0, 1]):

```
position(t) = t³·C0 + t²·C1 + t·C2 + C3
```

where C0…C3 are the matrix-transformed control point vectors.

**Color interpolation**: quadratic `lerp(startColor, endColor, t²)` — eases the color faster at the end of each edge.

**Buffer layout**:

| Buffer | Direction | Content |
|---|---|---|
| `InSplineData` | CPU→GPU | Per-spline metadata (endpoints, colors, type) |
| `InSplineSegmentData` | CPU→GPU | Segment index ranges |
| `InSplineControlPointData` | CPU→GPU | Control point positions |
| `OutSamplePointData` | GPU→GPU | Sampled positions + colors consumed by `BatchBSplineUnlit` |

All edge rendering is a single `Graphics.DrawProcedural(triangles, segments × 10 × 6)` call — zero per-edge CPU draw overhead.

**Why a compute shader and not CPU evaluation:**
B-spline evaluation is embarrassingly parallel — each segment is independent. On CPU, evaluating 10,000 segments × 10 samples = 100,000 cubic polynomial evaluations sequentially. On GPU with 32 threads/group, this runs in ~100 μs vs. multiple milliseconds on CPU, and the output stays on GPU memory — no round-trip upload to vertex buffer.

**Why 32 threads per group:**
GPU warps/wavefronts are 32 (NVIDIA) or 64 (AMD) threads wide. Setting numthreads(32,1,1) guarantees full warp utilization on NVIDIA with no divergence penalty. The dispatch count = ceil(segmentCount / 32).

---

## 4. Geometry Generation

### IcoSphere (`IcoSphere.cs`)
Procedurally generates sphere meshes for nodes.

- **Base**: 12 vertices from golden ratio `φ = (1 + √5) / 2`, 20 triangles
- **Subdivision**: midpoint refinement, each triangle → 4 triangles per pass
- **Detail levels**:

| Level | Triangles | Use case |
|---|---|---|
| 1 | 80 | Distant nodes |
| 2 | 320 | Default |
| 3 | 1 280 | Close-up / selected node |
| 4 | 5 120 | High quality mode |

- Normals = normalized vertex position (exact for sphere, no approximation)
- Mesh is cached — not rebuilt per node

**Why icosphere instead of UV sphere:**
UV spheres have triangle density concentrated at the poles and very elongated triangles near them — bad for specular highlights and silhouette quality. An icosphere distributes triangles nearly uniformly across the surface. For hundreds of nodes all visible at once, uniform distribution matters for consistent shading quality across viewing angles.

### Terrain Mesh (`FlatMesh.cs`)
2D Delaunay triangulation for the terrain surface.

- **Sunflower pattern**: 2 000 points distributed over the plane
- **Ridge points**: 50 additional points along community boundaries
- Heights applied separately via `HeightMap.cs`

**Why Delaunay triangulation:**
A uniform grid wastes triangles in empty areas and undersamples near community centers. Delaunay triangulation places vertices where they're needed (community boundaries, steep slopes) and maximizes the minimum angle of every triangle — avoiding needle-thin triangles that create Z-fighting and interpolation errors.

### HeightMap (`HeightMap.cs`)
CPU-side height field evaluated at each terrain vertex.

- Gaussian-like falloff centred on community nodes
- Peak height and falloff curve are `AnimationCurve`-configurable
- Tracks max node size and link weight for normalization

---

## 5. Texture Generation (CPU-side, dynamic)

All textures are rebuilt when the network data changes.

| Texture | Resolution | Method | Content |
|---|---|---|---|
| Height map | 480 × 480 | `GL.Begin(GL.TRIANGLES)` / `RenderTexture` | Gaussian height falloff per community |
| Albedo / node-color | 1 280 × 1 280 | CPU pixel loop | Closest-node colour per pixel, alpha-blended for overlaps |
| Line texture | 1 280 × 1 280 | CPU vector drawing | Edge lines coloured by link type |
| Normal map | 480 × 480 | Sobel filter over height map | Encoded as RGB: `(nx·0.5+0.5, ny·0.5+0.5, nz·0.5+0.5)` |
| Selection texture | same | CPU pixel loop | Highlighted node/edge tint |

**Sobel normal derivation**:
```
∂h/∂x = sample(x+1,y) − sample(x−1,y)
∂h/∂y = sample(x,y+1) − sample(x,y−1)
normal = normalize(−∂h/∂x, −∂h/∂y, 1)
```

**Why encode normals as RGB:**
Normal maps store a direction vector (x, y, z each in [-1, 1]) in a texture (each channel in [0, 1]). The formula `n * 0.5 + 0.5` remaps -1→0 and 1→1. In the shader, you reverse this: `normal = tex2D(_NormalMap) * 2.0 - 1.0`. The blue channel is typically high (z close to 1) which gives the characteristic blue tint of normal map textures.

**Why CPU texture generation instead of render-to-texture:**
The node color texture depends on knowing which node is closest to each pixel — a Voronoi-like computation. This is straightforward to write in C# with a spatial grid lookup. Moving it to a shader would require either ping-pong render passes or a Voronoi compute shader, adding significant complexity. Given that terrain textures only regenerate on data change (not every frame), CPU generation at 1280×1280 is acceptable.

---

## 6. Instanced Rendering

`NodeColorInstanced.shader` uses Unity GPU instancing:

```hlsl
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_INSTANCING_BUFFER_END(Props)
```

Per-node color is set via `MaterialPropertyBlock` (editor) or direct `material.color` (build), without creating separate material instances. Metallic and glossiness are uniform across all nodes in a batch.

**How GPU instancing works:**
The CPU uploads a buffer of per-instance data (color, transform matrix) to the GPU once. The draw call says "draw this mesh N times" with a single submission. The GPU runs N instances of the vertex shader in parallel, each reading its own row from the instance data buffer via `gl_InstanceID` / `SV_InstanceID`. This replaces N separate draw calls with 1, dramatically reducing CPU-GPU command overhead.

**MaterialPropertyBlock vs. separate materials:**
Creating a new Material per node would allocate N materials on the heap, each a GPU state object. The GPU driver would need to rebind material state on every node draw. `MaterialPropertyBlock` is a lightweight CPU-side override that slots into the existing material's instancing buffer — same material, different per-instance values.

---

## 7. Particle System — Minimap Overview (`OverviewRenderer.cs`)

The wrist-mounted minimap renders nodes as particles rather than meshes to stay cheap.

- `ParticleSystem.Particle[]` array, capacity 3 000
- Per-frame CPU update:
  - `particle.position` = world→local transform
  - `particle.startColor` = node colour
  - `particle.startSize` = scaled node size
  - `particle.remainingLifetime = float.MaxValue` (persistent)
- Two parallel particle systems: filled spheres + outline rings

**Why particles instead of instanced meshes for the minimap:**
The minimap needs to remain cheap because it's rendered every frame in addition to the main view. Particles use Unity's highly optimized billboard renderer — they're always camera-facing quads, drawn in a single GPU-batched call, and require no mesh geometry management. For a top-down overview where circular dots represent nodes, billboarded quads are visually sufficient and significantly cheaper than icosphere instances.

---

## 8. VR / XR Rendering

- **SDK**: Unity XR Interaction Toolkit v3.0.5 + Meta Quest XR plugin
- **Camera rig**: `XROrigin` — per-eye stereo rendering is handled transparently by URP
- **Movement**: `EasedDynamicMoveProvider` applies smooth easing to head-locked locomotion
- **FOV constant**: 100° used in minimap cone calculations (Meta Quest approximation)
- **Multi-display layout**: main viewport, handheld tablet network, wrist overview — independent transform hierarchies, not true stereoscopic splits

**How URP handles stereo rendering:**
URP renders two camera instances per frame — one per eye — each with a slightly offset view matrix (interpupillary distance offset). The projection matrix is asymmetric to match the lens geometry of each eye. All custom shaders and compute dispatches are written once and work for both eyes automatically, because URP handles the per-eye camera setup before invoking each render pass.

**VR-specific performance constraints:**
Quest targets 72 Hz minimum (120 Hz preferred). The total frame budget per eye is ~8 ms. This is why the edge rendering system was designed around a single compute dispatch + single draw call — any approach that scales CPU time with edge count would break this budget at network sizes > 1 000 edges.

---

## 9. Shared GPU Data Structures (`BSplineData.cginc`)

```
SplineData             56 bytes   index, segment count, endpoints, colors, type
SplineSegmentData      16 bytes   segment index, control point range, sample range
SplineSamplePointData  28 bytes   position (float3), color (float4), spline index
SplineControlPointData 12 bytes   position (float3)
```

Shared between the compute shader and `BatchBSplineUnlit.shader` via `StructuredBuffer` — no intermediate CPU readback.

**Why a shared .cginc header:**
The same struct layout must be binary-identical between the C# upload code, the compute shader, and the rendering shader. A single `.cginc` include file is the source of truth — changing the struct in one place changes it everywhere. This is the GPU equivalent of sharing a header between C++ modules.

---

## 10. Lighting & Visual Effects

| Effect | Where | How |
|---|---|---|
| Blinn-Phong | `NodeColorInstanced` | ambient + `max(0, dot(N,L))` diffuse + `pow(dot(N,H), shininess)` specular |
| Shadow receiving | `NodeColorInstanced` | `SAMPLE_SHADOW_MAP` from URP cascaded shadow atlas |
| Hover highlight | `NodeLinkRenderUtils` | `Color.Lerp(baseColor, hoverColor, 0.5)` |
| Selection outline | `SelectionTex` channel | additive screen blend on terrain |
| Terrain contour | `TerrainSurface` frag | `fmod(height, stepSize) < threshold` → discrete colour steps |
| Screen blend | `TerrainSurface` | `1 − (1−a)(1−b)` composites line + color layers |

---

## 11. Key Source Files

| File | Role |
|---|---|
| [Assets/Shaders/NodeColorInstanced.shader](Assets/Shaders/NodeColorInstanced.shader) | Node sphere lighting + instancing |
| [Assets/Shaders/CommColorInstanced.shader](Assets/Shaders/CommColorInstanced.shader) | Transparent community hulls |
| [Assets/Shaders/BatchBSplineUnlit.shader](Assets/Shaders/BatchBSplineUnlit.shader) | Ribbon edge rendering |
| [Assets/Shaders/BSpline.compute](Assets/Shaders/BSpline.compute) | GPU B-spline evaluation |
| [Assets/Shaders/TerrainSurface.shader](Assets/Shaders/TerrainSurface.shader) | Terrain texture compositing |
| [Assets/Shaders/Minimap.shader](Assets/Shaders/Minimap.shader) | FOV cone darkening |
| [Assets/Shaders/BSplineData.cginc](Assets/Shaders/BSplineData.cginc) | Shared GPU structs |
| [Assets/Scripts/renderers/BundledNetworkRenderer.cs](Assets/Scripts/renderers/BundledNetworkRenderer.cs) | Edge compute + draw dispatch |
| [Assets/Scripts/renderers/OverviewRenderer.cs](Assets/Scripts/renderers/OverviewRenderer.cs) | Particle-based minimap |
| [Assets/Scripts/renderers/TerrainNetworkRenderer.cs](Assets/Scripts/renderers/TerrainNetworkRenderer.cs) | Terrain mesh + texture pipeline |
| [Assets/Scripts/utils/IcoSphere.cs](Assets/Scripts/utils/IcoSphere.cs) | Icosphere mesh generation |
| [Assets/Scripts/utils/BSplineMathUtils.cs](Assets/Scripts/utils/BSplineMathUtils.cs) | Control point computation |
| [Assets/Scripts/utils/terrain/HeightMap.cs](Assets/Scripts/utils/terrain/HeightMap.cs) | Height field generation |
| [Assets/Scripts/utils/terrain/FlatMesh.cs](Assets/Scripts/utils/terrain/FlatMesh.cs) | Delaunay terrain mesh |

---

---

# Interview Q&A — Nuro Autonomy Visualization Role

---

## Graphics Fundamentals

**Q: Walk me through the graphics rendering pipeline.**

The classic pipeline: application (CPU) → geometry stage → rasterization → fragment stage → output merge.

More specifically: the CPU submits draw calls with vertex buffers and shader programs. The **vertex shader** runs once per vertex — transforms positions from model space → world → view → clip space, and computes per-vertex data like normals and UVs. The **rasterizer** interpolates those values across the triangle's pixels and generates fragments. The **fragment shader** runs once per fragment — computes final color using lighting, textures, and material properties. The **output merger** applies depth testing (`Z-buffer`), stencil testing, and alpha blending before writing to the framebuffer.

In this project, the edge rendering pipeline adds a compute shader stage before the draw: GPU evaluates spline positions and writes them to a buffer, then the vertex shader reads that buffer to construct ribbon geometry — all without any CPU involvement after the initial dispatch.

---

**Q: What is the difference between a vertex shader and a fragment shader? When would you do work in one vs. the other?**

A vertex shader runs once per input vertex. A fragment shader runs once per screen pixel covered by a triangle — which can be millions of times vs. thousands.

Rule of thumb: do as much work as possible in the vertex shader. Lighting calculations involving geometry normals can often be done per-vertex and interpolated (Gouraud shading) instead of per-fragment (Phong shading) — cheaper, slightly less smooth. Texture lookups, fine surface detail, and effects that need per-pixel precision (specular highlights, normal mapping) must be in the fragment shader.

In `BatchBSplineUnlit`, the entire ribbon geometry construction — computing tangents, offsetting vertices into a quad — happens in the vertex shader. The fragment shader just reads interpolated color and discards near-transparent fragments. This is intentional: geometry work in vertex = one call per vertex; moving it to fragment would be one call per covered pixel.

---

**Q: Explain depth testing and the Z-buffer. What problems can arise?**

The Z-buffer stores the depth value of the closest fragment written so far at each pixel. When a new fragment arrives, its depth is compared against the stored value. If it's closer, it passes and updates the buffer; otherwise it's discarded. This gives correct occlusion without requiring back-to-front sorting.

Problems:
- **Z-fighting**: two surfaces at nearly the same depth compete for the same Z-buffer precision, causing flickering. Common with coplanar geometry (e.g., a decal lying on a surface). Solutions: polygon offset, increase depth buffer precision (32-bit vs 16-bit), separate the surfaces.
- **Transparent geometry sorting**: if a transparent object writes to the Z-buffer (`ZWrite On`), it blocks opaque geometry behind it from rendering. This is why community hull shaders in this project use `ZWrite Off` — transparent surfaces shouldn't occlude.
- **Depth precision loss at distance**: depth buffer precision is not linear — it's very precise near the near clip plane and coarse at the far plane. For a large scene, set the near clip plane as far as practically possible.

---

**Q: What coordinate spaces are involved in 3D rendering? How do you transform between them?**

1. **Model/Object space** — vertex positions relative to the mesh's own origin
2. **World space** — positions in the shared scene after applying the model's transform matrix (translation, rotation, scale)
3. **View/Camera space** — world positions relative to the camera; camera sits at origin looking down -Z
4. **Clip space** — after applying the projection matrix (perspective or orthographic); vertices outside the view frustum are clipped
5. **NDC (Normalized Device Coordinates)** — after perspective division (`xyz / w`); visible volume is [-1,1]³
6. **Screen space** — final pixel coordinates after viewport transform

In shaders: `clip_pos = UNITY_MATRIX_MVP * model_pos`, which is `Projection × View × Model × vertex`. Normal vectors transform differently from positions — they use the inverse-transpose of the model matrix to preserve perpendicularity when the mesh is non-uniformly scaled.

---

**Q: What is GPU instancing? How does it work at the hardware level?**

GPU instancing lets you draw the same mesh N times in a single draw call. The CPU uploads one buffer containing per-instance data (transform matrices, colors, etc.), then issues one `DrawInstanced(mesh, N)` call. The GPU runs N instances of the vertex shader in parallel, each reading from a different row of the instance data buffer using `SV_InstanceID`.

At the hardware level, the GPU's command processor reads the instance count, creates N work items, and distributes them across shader cores. There's no serial loop on the CPU — the GPU processes all instances in parallel. This is orders of magnitude faster than N separate draw calls, each of which requires a CPU→GPU command buffer submission, state validation, and driver overhead.

In this project, `NodeColorInstanced.shader` uses instancing with `MaterialPropertyBlock` to give each node a unique color while sharing the same shader and mesh. Without instancing, 5,000 nodes = 5,000 draw calls; with instancing, 5,000 nodes = 1 draw call.

---

**Q: What is a compute shader? How is it different from vertex/fragment shaders?**

A compute shader is a GPU program that runs outside the graphics pipeline — it has no fixed-function inputs (no vertices, no rasterizer) and no direct framebuffer output. You dispatch it explicitly as a grid of thread groups, and it reads/writes arbitrary buffers and textures.

Differences from graphics shaders:
- No pipeline stage constraints — it can both read and write the same buffer
- No rasterization — it's pure data processing
- Explicit thread group sizing — you control parallelism directly
- Output goes to `RWBuffer` / `RWTexture`, not the framebuffer

Use cases: physics simulation, particle updates, image processing, and in this project, B-spline curve evaluation. The compute shader dispatches 32 threads per group, each evaluating one spline segment independently. The output structured buffer is then consumed directly by the rendering shader — no CPU readback needed, data stays on GPU.

---

## Project-Specific Questions

**Q: You mentioned all edges are drawn in a single draw call. How exactly does that work?**

The key is `Graphics.DrawProcedural`. Instead of submitting a vertex buffer pre-filled with geometry, you tell the GPU "draw N vertices" and let the vertex shader synthesize the geometry on the fly using `SV_VertexID`.

Here's the breakdown:
1. Compute shader fills `OutSamplePointData[]` — one entry per spline sample, containing world position and color
2. `BatchBSplineUnlit` vertex shader receives `SV_VertexID` from 0 to `(totalSegments × 10 × 6) - 1`
3. Vertex ID → sample index: `sampleIdx = vertexID / 6`
4. Within the quad: `cornerIdx = vertexID % 6` (0-5 = two triangles)
5. Shader reads `OutSamplePointData[sampleIdx]`, computes tangent from adjacent samples, offsets position to the correct corner of the billboard quad
6. All this runs in parallel across all vertices simultaneously

The CPU never touches individual edge geometry. Submitting one draw call vs. 10,000 eliminates essentially all CPU overhead for edge rendering.

---

**Q: How did you handle the performance budget for VR?**

VR on Quest has a hard 8 ms/frame budget per eye at 72 Hz, which doesn't leave room for per-object overhead.

The major optimizations:
- **Edges**: single compute dispatch + single `DrawProcedural` regardless of edge count. Going from 10,000 draw calls to 1 was the biggest win.
- **Nodes**: GPU instancing with `MaterialPropertyBlock` batches all nodes in one draw call per material variant
- **Minimap**: particle system instead of instanced meshes — Unity's particle renderer is extremely optimized for Billboard quads
- **Terrain**: static mesh with dynamic textures — the mesh doesn't change every frame, only the textures when data updates, and texture updates are amortized (not per-frame)
- **Icosphere caching**: mesh generated once and shared across all node instances

Profiling was done with Unity's Frame Debugger and GPU Profiler, looking for draw call count, GPU time per pass, and memory bandwidth.

---

**Q: How would you approach adding a new visualization element — say, a glowing halo around selected nodes?**

This is a good design question. My approach:

1. **Define the data need**: the renderer needs to know which nodes are selected. The existing selection state is already tracked in `NodeLinkRenderUtils`. I'd expose a `selectedNodeIds: HashSet<int>` to the renderer.

2. **Choose the rendering strategy**: for a halo/glow, there are a few options:
   - A second instanced draw pass with a slightly scaled-up sphere, backface-only, colored differently (shell method — cheap, no post-processing)
   - A bloom/glow post-process pass (URP `RenderFeature`) — looks better, costs more
   - A screen-space outline using depth discontinuity detection (Roberts cross edge detection in a fullscreen pass)

3. **Implement the simplest that meets quality bar**: I'd start with the scaled-up backface shell — it's one additional instanced draw call, no new render passes, and works in VR without stereo reprojection artifacts that post-processing can introduce.

4. **Hook it into the existing pipeline**: add a new `MaterialPropertyBlock` property for halo color and scale, update `BundledNetworkRenderer.UpdateRenderElements()` to set it for selected nodes, and add the second draw call in `BSplineShaderWrapper.Draw()`.

The system is already structured for this — the renderer is separated from the data layer, so adding a new visual element is additive, not invasive.

---

**Q: How does this experience transfer to autonomous driving visualization?**

The core problems are the same:

| This project | AV visualization at Nuro |
|---|---|
| Thousands of graph edges (relationships) | Thousands of LiDAR points (point cloud) |
| Single draw call via `DrawProcedural` | Instanced point rendering or compute-driven draw |
| B-spline GPU evaluation | Trajectory/prediction curve evaluation |
| Node color = graph attribute | Point color = depth / intensity / classification |
| VR real-time budget (8 ms/frame) | Real-time replay at 60+ Hz |
| Custom terrain from graph data | HD map / occupancy grid rendering |
| Multi-view (main + minimap + tablet) | Multi-sensor view (camera + LiDAR + radar overlaid) |

The specific GPU techniques — compute shaders for data processing, structured buffers for CPU→GPU transfers, single draw calls for large datasets, instancing for repeated geometry — all transfer directly. The domain changes (graph → sensor data) but the performance engineering approach is identical.

For LiDAR point clouds specifically: a compute shader could classify and color 100k+ points per frame; `DrawProceduralIndirect` could draw only visible points after GPU-side frustum culling — all patterns I've already implemented in this project.

---

## Concepts You Should Be Able to Explain Off the Top of Your Head

**Q: What is a render target / framebuffer? What is a G-buffer?**

A render target is a texture the GPU writes to instead of (or in addition to) the screen. Framebuffer = the set of render targets for the current pass (typically color + depth).

A G-buffer (Geometry Buffer) is used in **deferred rendering**: in the first pass, geometry writes its properties (albedo, normals, depth, metallic, roughness) to multiple render targets simultaneously (MRT). In the second pass, lighting is computed in screen space using those stored properties. Benefit: lighting cost scales with screen resolution, not scene complexity. Cost: high memory bandwidth, doesn't work well with transparency or MSAA.

**Q: What is MSAA? What is TAA?**

MSAA (Multisample Anti-Aliasing): the rasterizer evaluates coverage at multiple sub-pixel sample points per pixel but runs the fragment shader only once per pixel. Reduces geometric aliasing (jagged edges) cheaply. Doesn't help with shader aliasing (thin features, specular highlights).

TAA (Temporal Anti-Aliasing): jitters the projection matrix slightly each frame and accumulates results over multiple frames using a reprojection buffer. Effectively super-samples over time. Smoother results than MSAA including shader aliasing, but introduces ghosting/blurring on fast motion. Standard in modern real-time renderers.

**Q: What is alpha blending and what are the blend equation options?**

Alpha blending combines a source fragment's color with the existing framebuffer color using:
`output = srcColor × srcFactor + dstColor × dstFactor`

Common modes:
- **Standard transparency**: `src=SrcAlpha, dst=OneMinusSrcAlpha` → `output = src.rgb × src.a + dst.rgb × (1 - src.a)` — used for community hulls
- **Additive**: `src=One, dst=One` → colors add together, makes things glow/brighten — useful for glow effects, light shafts
- **Multiplicative**: `src=DstColor, dst=Zero` → output = src × dst, darkens — used for shadows, darkening effects

Transparency requires back-to-front sorting for correct results (transparent objects must be drawn from farthest to nearest). This is why transparent objects in the project have `ZWrite Off` — they don't block the depth buffer, but they still need correct sorting.

**Q: What is the difference between diffuse and specular reflection?**

**Diffuse** (Lambertian): light scatters equally in all directions after hitting a surface. Intensity = `max(0, dot(N, L))` — only depends on the surface normal and light direction, not the viewer. Matte surfaces like clay or unfinished wood are predominantly diffuse.

**Specular**: light reflects preferentially in the mirror direction. Intensity depends on the viewer position. Blinn-Phong approximates it as `pow(dot(N, H), shininess)` where H = normalize(L + V) is the half-vector. High shininess = tight, sharp highlight (metal). Low shininess = broad, soft highlight (plastic). The nodes in this project use shininess ~32 (configurable), giving a soft plastic look.

PBR (Physically Based Rendering) replaces these with the Cook-Torrance microfacet BRDF, which is physically grounded and gives realistic results under any lighting condition — used in `BasicLit.shader` for environment objects.

---

## Questions to Ask the Interviewer

- What does the visualization data pipeline look like? Does sensor data come in as ROS messages, protobuf, or a custom format?
- Are visualizations primarily for internal tooling (debugging autonomy algorithms), or do some ship in the vehicle?
- What's the biggest current performance bottleneck — data throughput, rendering, or latency?
- How does the team balance visualization accuracy (showing exactly what the autonomy stack sees) vs. interpretability (making it readable for engineers)?
- Is OpenGL/Qt the primary stack for in-vehicle visualization, or is Unity used there too?
