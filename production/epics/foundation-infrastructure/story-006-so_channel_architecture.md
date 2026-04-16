# Story 006: SO Channel Architecture — Foundation Event Bus

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`（ViewLayerChannel）；框架级事件规范
**Requirement**: `TR-event-001`, `TR-event-002`, `TR-event-003`

**ADR Governing Implementation**: ADR-0002 — Three-Tier Communication Architecture
**ADR Decision Summary**: 跨场景用 SO Channel（Tier 1）；OnEnable/OnDisable 订阅配对（MANDATORY）；异步 UniTask 传 destroyCancellationToken。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: Tier 1 跨场景事件必须用 SO Channel，存于 `assets/data/channels/`
- Required: 所有订阅必须在 OnEnable/OnDisable 配对
- Required: 所有跨 await 的 async UniTask 必须传 this.destroyCancellationToken

---

## Acceptance Criteria

*From ADR-0002 Implementation Guidelines:*

- [ ] ADV-01/ADV-02：所有 SO Channel 订阅使用 OnEnable/OnDisable 对，禁止 Awake/Start 订阅
- [ ] ADV-03：所有跨 await 的 UniTask 方法传入 `this.destroyCancellationToken`
- [ ] 所有 Channel 资产位于 `Assets/Data/Channels/`
- [ ] Channel Raise() 调用路径：Raise() → event?.Invoke()，等价于直接 C# event 调用，零反射
- [ ] Channel 内存：~200B/channel，可忽略不计

---

## Implementation Notes

*From ADR-0002:*

1. **SO Channel 基类**：
   ```csharp
   [CreateAssetMenu(menuName = "Channels/GameEvent")]
   public class GameEvent : ScriptableObject {
       private event Action<object> Event;
       public void Raise(object payload) => Event?.Invoke(payload);
       public void Subscribe(Action<object> handler) => Event += handler;
       public void Unsubscribe(Action<object> handler) => Event -= handler;
   }
   ```
   实际项目建议用泛型版本 `GameEvent<T>` 避免 boxing。

2. **订阅模板**：
   ```csharp
   private void OnEnable() => _channel.Subscribe(OnPayload);
   private void OnDisable() => _channel.Unsubscribe(OnPayload);
   private void OnPayload(object p) => /* handle */;
   ```

3. **destroyCancellationToken 传递**：
   ```csharp
   public async UniTask FooAsync() {
       await SomeAsyncOperation(destroyCancellationToken);
   }
   ```

4. **Channel 资产清单**（在本 Epic 中创建，Epic B 消费）：
   - `Assets/Data/Channels/ViewLayerChannel.asset`
   - `Assets/Data/Channels/ShipStateChannel.asset`
   - `Assets/Data/Channels/SimRateChangedChannel.asset`
   - `Assets/Data/Channels/CombatChannel.asset`
   - `Assets/Data/Channels/ColonyShipChannel.asset`

---

## Out of Scope

- 具体 Channel 的广播逻辑（由 Epic B 和 Core 层的故事实现）
- 具体系统的订阅者（由 Epic B 和 Core 层的故事实现）

---

## QA Test Cases

- **ADV-01/ADV-02: OnEnable/OnDisable 订阅配对**
  - Given: MonoBehaviour 订阅 GameEvent
  - When: OnEnable 调用 Subscribe；OnDisable 调用 Unsubscribe
  - Then: OnDisable 后再次 Raise 不触发 handler；无内存泄漏

- **ADV-03: destroyCancellationToken 传递**
  - Given: async UniTask 方法，组件 attached to GameObject
  - When: GameObject 被 Destroy 时，有 pending async operation
  - Then: 等待中的操作正确取消（不触发 NullReferenceException）

- **Channel Raise 零反射验证**
  - Given: GameEvent 实例
  - When: 100 次连续 Raise
  - Then: Profiler 中无反射开销；GC 分配 < 1KB

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/event/channel_architecture_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（Channel 架构独立）
- Unlocks: Story 007（具体 Channel 广播/订阅）；Epic B 所有依赖 SO Channel 的系统
