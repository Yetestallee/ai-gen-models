# Meshy Workspace

A Unity package for AI-powered 3D model generation, image generation, and animation using the Meshy API.

## Features

- **AI 3D Model Generation** — Text-to-3D, Image-to-3D, Re-texture, Re-mesh, Rigging
- **AI Image Generation** — Text-to-Image with multi-view support
- **Animation** — AI-driven animation generation
- **Runtime Preview** — 3D model preview with orbit controls, animation playback
- **Task Management** — Queue, poll, and cache AI generation tasks
- **SSE Progress Tracking** — Real-time progress via Server-Sent Events
- **Windows Build Pipeline** — One-click build with bundled assets

## Installation

### Via Git URL (UPM)

Add the following to `Packages/manifest.json`:

```json
"com.besty.meshy-workspace": "https://github.com/Besty0728/com.besty.meshy-workspace.git"
```

### Via Local Path

```json
"com.besty.meshy-workspace": "file:../com.besty.meshy-workspace"
```

## Dependencies

| Package | Version |
|---------|---------|
| `com.unity.cloud.gltfast` | 6.14.1 |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 |
| `com.unity.ugui` | 1.0.0 |

## Sample

This package includes a **Meshy Game** sample scene that demonstrates the full runtime workspace. Import it via:

1. Window → Package Manager → Meshy Workspace
2. Expand **Samples** → **Meshy Game**
3. Click **Import**

## Quick Start

1. Import the sample scene
2. Open `MeshyGame/Scenes/MeshyGame.unity`
3. Configure your Meshy API key in the **Meshy Workspace** window (Tools → Meshy Workspace → Settings)
4. Enter Play Mode

## License

MIT