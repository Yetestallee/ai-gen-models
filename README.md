# AI Gen Model

基于 Unity 的 AI 3D 模型生成工作台，集成 [Meshy API](https://www.meshy.ai)，支持文生图、文生 3D、图生 3D、重贴图、重拓扑、骨骼绑定、动画生成等功能。

## 项目结构

```
ai-gen-models/
├── Packages/
│   └── com.besty.meshy-workspace/    ← UPM 包（核心代码 + 示例场景）
│       ├── Runtime/                   ← 运行时：API 客户端、任务管理、模型预览
│       ├── Editor/                    ← 编辑器：构建管线、设置窗口
│       ├── Samples~/MeshyGame/       ← 示例场景（可导入其他项目）
│       └── Documentation~/README.md   ← 包文档
├── ProjectSettings/                   ← Unity 项目设置
└── Packages/manifest.json             ← 包依赖清单
```

## 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/Yetestallee/ai-gen-models.git
```

### 2. 在 Unity 中打开

- 使用 **Unity 2022.3.62f3c1** 或更高版本打开项目
- 确保安装以下模块：
  - Windows Build Support (IL2CPP)
  - WebGL Build Support（可选）

### 3. 运行场景

打开 `Packages/com.besty.meshy-workspace/Samples~/MeshyGame/Scenes/MeshyGame.unity`，点击 Play 运行。

### 4. 配置 API Key

1. 菜单栏 → **Meshy Workspace** → **Settings**
2. 填入你的 Meshy API Key（从 [meshy.ai](https://www.meshy.ai) 获取）
3. 点击 **Save**

## 功能一览

| 功能 | 说明 |
|------|------|
| 🖼️ 文生图 (Text-to-Image) | 输入提示词生成多张图片，支持多视图 |
| 🏗️ 文生 3D (Text-to-3D) | 文本描述生成 3D 模型（GLB） |
| 📷 图生 3D (Image-to-3D) | 参考图片生成 3D 模型 |
| 🎨 重贴图 (Re-texture) | 修改模型贴图样式 |
| 🔄 重拓扑 (Re-mesh) | 优化模型面数与拓扑结构 |
| 🦴 骨骼绑定 (Rigging) | 自动生成骨骼动画 |
| 🎬 动画生成 (Animation) | AI 驱动动画生成 |
| 🖥️ 3D 预览 | 运行时模型 3D 预览（旋转、缩放） |
| 📋 任务管理 | 任务队列、进度追踪、历史记录 |
| 🏗️ Windows 构建 | 一键构建带内嵌资源的 Windows 可执行文件 |

## 构建

### Windows 构建

菜单栏 → **Meshy Workspace** → **Build Windows Exe**

输出到 `Builds/MeshyGame/MeshyGame.exe`，自动附带生成的模型和历史记录。

## 以 UPM 包方式安装到其他项目

在目标项目的 `Packages/manifest.json` 中添加：

```json
"com.besty.meshy-workspace": "https://github.com/Yetestallee/ai-gen-models.git?path=/Packages/com.besty.meshy-workspace"
```

然后通过 **Window → Package Manager → Meshy Workspace → Samples → Import** 导入示例场景。

## 依赖

| 包 | 版本 |
|----|------|
| `com.unity.cloud.gltfast` | 6.14.1 |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 |
| `com.unity.ugui` | 1.0.0 |
| `com.besty.unity-skills` | Git 引用 |

## 技术栈

- **Unity 2022.3 LTS**
- **UI Toolkit** — 运行时 UI
- **glTFast** — GLB 模型加载与渲染
- **Newtonsoft.Json** — API 数据序列化
- **Meshy API** — AI 生成服务

## 许可

MIT