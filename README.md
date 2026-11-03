# Common Utils for Unity

面向 Unity 2022.3 LTS 的可复用工具包。新增模块不依赖具体玩法、Photon、YooAsset、DOTween、Odin 或 UltimateXR，不创建工具菜单，不在导入时改场景或自动启动采样。

## 安装

Unity Package Manager → Add package from git URL：

```text
https://github.com/BrokenMyth/com.jcommonutils.unity.git#v1.1.0
```

团队项目可把版本标签换成完整 commit SHA，并提交 `Packages/manifest.json` 与 `packages-lock.json`。不要在项目 Packages 中再放同名内嵌目录，也不要直接编辑 Library/PackageCache。

最低支持版本调整为 **Unity 2022.3**；此版本的验证基线为 2022.3.56f1。旧的 2020.3 声明不代表已覆盖验证；如需旧 Unity，应先完成 API/语言兼容，不直接使用此版本。

## 模块与依赖

| 程序集 | 内容 | 依赖边界 |
| --- | --- | --- |
| `JCommonUtils.Core` | 层级查找、水平扇形、预制体池生命周期 | Unity/.NET；禁用隐式第三方 DLL 引用 |
| `JCommonUtils.Editor.Diagnostics` | 显式性能采样、统计与 JSON/HTML 报告 | 仅 Editor，Unity/.NET，不进入 Player |
| `CommonUtils` | 已有事件、单例及旧工具 API | 保留旧命名空间；旧 JSON/UI 工具所需 Unity Newtonsoft/UGUI 已显式声明 |

旧 `System.EventCenterSystem`、`MonoSingleton<T>`、`Singleton<T>` 等公开入口不改名。新 API 使用 `JCommonUtils.*`，由上层提供资源、玩法和第三方适配。包中原 Newtonsoft DLL 保留可覆盖属性以兼容既有布局，实际使用 Unity NuGet 包提供的版本；不要再人为添加同名非可覆盖 DLL。

有 asmdef 的调用方需显式引用相应程序集；没有 asmdef 的 Assembly-CSharp 可直接使用运行时核心。不要让运行时 asmdef 引用 Editor.Diagnostics。

## 使用与示例

- [层级查找与几何](Documentation~/HierarchyGeometry.md)：包含/排除根节点的 DFS 查找，世界 XZ 扇形判定与 Gizmo。
- [对象池](Documentation~/Pooling.md)：租用身份、复用代次、策略回调、延迟回收和主动释放。资源加载、特效/Tween 全量重置、网络身份管理不属于默认核心。
- [性能采样与报告](Documentation~/Performance.md)：显式启动/结束采样、缺失计数器、样本容量、JSON/HTML 导出及数据解释。

Package Manager 的 Samples 提供 `Core Basics` 和 `Editor Performance Capture`。导入后按各文档执行；不会自动创建发行版相机、添加菜单、连接服务器或修改画质。

## 测试

包内 `Tests/Editor` 是实际可运行的测试程序集，覆盖事件契约、查找语义、几何边界、池生命周期、统计和报告。它们不引用 Mecha 的场景、表、存档或联机协议。

消费项目安装 Unity Test Framework，并在项目 manifest 中添加：

```json
"testables": ["com.jcommonutils.unity"]
```

在 Test Runner 的 EditMode 运行 `JCommonUtils.Tests.Editor`。部分生命周期测试会进入/退出 PlayMode；测试前保存场景。命令行可使用 `-runTests -testPlatform editmode -assemblyNames JCommonUtils.Tests.Editor -testResults <path>`，不要同时传 `-quit` 提前结束测试。

1.1.0 已在 Windows / Unity 2022.3.56f1 独立项目验证：42 个包内测试通过；两个导入示例完成实际播放、对象租还/清理和 30 秒采样的 JSON/HTML 导出。该结果验证工具功能，不代表消费项目或目标设备的帧率收益。

## 性能与生命周期约定

- 查找用于初始化/重绑定，O(N)；Unity 名称读取不承诺零分配，不推荐逐帧查找。
- 几何判断 O(1)，不读取玩法或网络状态。
- 池在 Unity 主线程创建/调用；容量、租用版本、回收策略与资源所有权见对象池文档。创建/扩容/异步调度等生命周期变更路径不承诺零 GC。
- 采样器只在显式请求后订阅 Editor 更新；停止、退出播放和域重载会释放采样资源。报告统计、排序及文件 I/O 属于结束后的冷路径。
- 编辑器采样不能代替目标设备验收，也不能把帧间隔当 GPU 执行时间或因果性的优化收益。

## 维护

在本仓库修改与测试，再发布新版本/提交；消费项目更新 Git 引用。迁移已有 MonoBehaviour 或生命周期适配器时，保留 GUID/序列化引用并验证行为。基础/AOT 程序集更新需要重新构建 Player；仅业务热更不能替换这些实现。
