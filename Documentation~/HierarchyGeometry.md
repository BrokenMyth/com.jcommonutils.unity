# 层级查找与水平扇形

运行时代码属于 `JCommonUtils.Core`，仅使用 System/Unity，不依赖 Mecha、Odin、Photon 或资源加载框架。

## 层级查找

```csharp
using JCommonUtils.Hierarchy;

Transform hand = TransformSearch.FindChildRecursive(character, "Hand");
Transform rootOrHand = TransformSearch.FindSelfOrChildRecursive(character, "Hand");
```

- `FindChildRecursive` 排除传入根节点；`FindSelfOrChildRecursive` 包含根节点。
- 两者均按层级顺序深度优先返回首个精确名称匹配，区分大小写，包含失活节点。
- 空/已销毁根节点返回 null，空名称仍可匹配实际空名称节点；null 名称不会匹配正常节点。
- 不缓存结果，下一次调用会看到重命名、重挂接、增删和对象池复用后的当前层级。
- O(N) 遍历、O(H) 递归栈，N 为子树节点数、H 为深度；不创建临时集合或枚举器，Unity 名称访问不承诺零分配。用于初始化/重绑定，不建议每帧重复查找。调用方管理已缓存引用的生命周期。

已有项目迁移时不要机械替换全部同名方法。某些旧方法检查根节点，另一些只检查后代，须选择对应入口。

## 世界水平扇形

```csharp
using JCommonUtils.Geometry;

bool contains = HorizontalSector.ContainsWorldPoint(
    anchor, Vector3.zero, 5f, 90f, true, 1.5f, target.position);
```

参数依次为锚点、局部原点偏移、世界半径、完整张角（度）、是否检查高度带、世界半高度、待测世界坐标。

- 固定使用世界 XZ 平面与世界 Y 高度，不是任意 Transform 的局部平面。
- 原点偏移通过 `TransformPoint`，包含锚点缩放；半径与半高度已经是世界单位，不会再次乘缩放。
- 张角夹到 1–360 度，半径/半高度最小 0.01，中心容差 0.001，角度边缘容差 0.01 度。先检查高度和半径，再应用中心快捷判断。
- 前向投影过短时回退世界 +Z。锚点为空/已销毁时判定为 false；`GetOrigin` 则将偏移视为世界坐标。
- 计算 O(1)，无托管临时集合或对象。它不包含遮挡、导航、阵营、网络权威或伤害判定。

从 `OnDrawGizmos` 调用 `HorizontalSectorGizmos.Draw` 可显示径向线与轮廓；分段数限制在 8–90，绘制后恢复原 Gizmos 颜色，不新增编辑器菜单。

## 示例与验证

导入 Basics 示例，把 `HierarchyGeometryExample` 挂到空物体后播放：它创建两个自己的子节点，演示失活节点仍可查找，并输出一次英文结果。Scene 视图可显示扇形 Gizmo。对象随父节点销毁，无持久化资源。

包测试 `HierarchyTests` 覆盖根包含规则、DFS 重名顺序、失活、改名/重挂接/销毁、名称边界；`HorizontalSectorTests` 覆盖半径/角度/高度边界、缩放后的原点、世界半径、竖直朝向回退和空锚点。测试只创建临时 GameObject，不读取项目场景和预制体。
