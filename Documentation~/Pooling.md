# 预制体对象池

命名空间 `JCommonUtils.Pooling`，程序集 `JCommonUtils.Core`。只依赖 Unity / System；不要求 YooAsset、DOTween、Photon、Odin 或项目单例。

```csharp
// prefab 是已经加载的 GameObject。拥有者负责在场景退出时 Dispose。
var pool = new PrefabPool(prefab, maximumCached: 64);
PoolLease lease = pool.Rent(position, Quaternion.identity);
lease.ReturnAfter(2f);

// 拥有者 Update：只有这个入口处理定时/自定义寿命，失活的租用实例也会到期。
pool.Tick();

// 可以提前归还；重复归还或拿上一代凭据归还新实例均返回 false。
lease.TryReturn();
pool.Dispose();
```

## 契约

- 在 Unity 主线程创建及使用。后续操作会检查创建线程，但该检查不能把工作线程变成 Unity 主线程。
- 默认创建失活缓存容器；也可传入外部失活 `Transform storage`，池只销毁自己创建的容器。
- `maximumCached` 只限制闲置缓存数量，不限制同时活跃数量。`ActiveCount/CachedCount` 表示当前登记槽位；外部销毁的实例由下一次 Tick / Rent 清理。
- `PoolSpawn` 提供世界/本地姿态及初始激活开关。默认准备路径恢复实例创建完成时的根局部缩放，再设置根位置、旋转和挂接：世界租用保留该可视尺寸，局部租用使用该局部缩放并接受父缩放影响，重复世界租用或切换姿态不会累积父缩放。不恢复子节点、Collider、Rigidbody、材质、粒子或业务字段。
- 覆盖 `PrefabPoolPolicy.Prepare` 时，外层完全负责复用状态；调用 `base.Prepare` 可复用上述默认根缩放契约。独立调用 `PoolSpawn.ApplyTo` 仍保留目标当前局部缩放，供显式自定义策略使用，不隐式改写已有策略的缩放规则。
- `PoolLease` 是一次租用的值类型凭据。`Version` 每次递增；旧凭据的 `Instance` 为 null，`TryReturn/ReturnAfter` 返回 false。异步回调应保存当时的凭据，不要重新寻找“当前租用”后回收。
- `ReturnAfter` 使用 `Time.timeAsDouble`（受 timeScale 影响），负数视为零，拒绝 NaN/Infinity；租用当帧不会由 Tick 到期。再次租用会清除旧寿命。池不创建自己的 Update 或隐藏计时器。
- 普通回收遇到失活父层级会通过创建时的 Unity `SynchronizationContext` 延后，防止 `OnDisable` 中改父级。一个实例最多一个已排队回调，复用 Entry 状态而非装箱临时请求；归还、再租用和 Dispose 会撤销旧请求。需要有效 Unity 主线程上下文。
- `Tick` 应从正常 Update 调用，不应从层级显隐回调调用。高级适配可关闭父节点停用延后，但必须自行保证原生层级操作时序安全。
- 已有缓存情况下，默认租还和身份查询 O(1)；Tick 是 O(N)，N 为当前活跃数量，活跃规模由消费方约束。首次 Instantiate/Entry、活跃数组扩容、异常、业务策略及 Unity 内部 Post 可能分配，不能把整套池宣传成所有情况下零 GC。

## 业务状态和 SDK 适配

可选 `PrefabPoolPolicy` 由一个池复用：

| 回调 | 时机及职责 |
| --- | --- |
| `OnCreated` | 实例首次创建、失活时缓存组件或初始化状态。不得激活、重新挂接或销毁该实例，避免在姿态和租用状态准备前触发 OnEnable。 |
| `Prepare` | 租用身份有效，激活前准备姿态及状态；默认恢复创建时根缩放。覆盖时可调用 `base.Prepare`，或自行完成姿态与缩放设置。 |
| `CanReturn` | 普通回收资格。未提交的业务事务可拒绝回收，Dispose 与准备失败不受此门槛阻止。 |
| `ShouldReturn` | 可选自定义寿命，例如粒子全部结束；只由 Tick 调用。 |
| `OnReturn` | 身份已撤销、物体已停用后清理资源及业务状态。 |

策略不能将实例交给其他池。准备/激活回调主动归还同一凭据会让 Rent 明确失败，不能返回已经失效的对象。归还清理异常会销毁该实例并向调用者抛出，池保持可继续租用；Dispose 会完成全部清理后汇总抛出回调异常，重复 Dispose 无作用。

资源加载及 AssetHandle 生命周期由 YooAsset 等外层适配器维护；Tween、特效、音频、网络对象、存档/库存规则均不进入核心。Mecha 的旧 `PooledObject` 保留表现状态复位及 DOTween 逻辑，作为策略适配使用同一池核心。

## 示例与测试

从 Package Manager 导入 Basics 示例，将 `PoolExample` 挂到空物体即可运行，不依赖素材或菜单。

`PrefabPoolTests` 覆盖代次与重复回收、默认不重置业务状态、两种姿态、缓存容量、外部销毁、资格阻止、异常恢复、重入、批量释放及线程边界；进入播放模式的用例使用真实父节点停用回调和延后消息，验证旧请求隔离及失活对象到期。
