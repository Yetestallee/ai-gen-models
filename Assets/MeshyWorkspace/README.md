# Meshy Workspace

Unity 2022.3 编辑器插件：把 Meshy API 的图像 / 模型生成流程搬进 Unity 工作区。

## 目录

- `Runtime/`：可复用逻辑（P1 起加入 API 客户端、DTO、任务引擎）
- `Editor/`：EditorWindow、设置、P0 连通性探针
- `Tests/`：EditMode 单元测试

## 依赖

- `com.unity.cloud.gltfast` 6.14.1（3D 预览）
- `com.unity.nuget.newtonsoft-json` 3.2.1（JSON）

## 配置

`Menu > Meshy Workspace > Settings...` 保存 API Key 与代理地址；密钥存 EditorPrefs，不进入项目文件、日志或仓库。

## P0 冒烟

`Menu > Meshy Workspace > Test API Connection` 调用 `GET /openapi/v1/balance`，结果写入 `Library/MeshyWorkspace/p0-balance-report.txt`。
