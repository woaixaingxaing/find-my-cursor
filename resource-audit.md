# FindMyCursor 资源影响最终审查（优化后）

审查对象：`%LOCALAPPDATA%\InputCursorIndicator\FindMyCursor.exe`
构建：优化后构建（镜头缓冲复用 + grow 帧反向复用 + ShakeDetector 环形缓冲 + 校准后停 TIA + 日志轮转）
审查日期：2026-11-02
环境：虚拟屏幕 4440×1920，Windows 11，.NET Framework 4.x

> 项目没有保留“修改前”的旧二进制，无法做严格同机 A/B。本文以**核心空闲常驻**为基线，按代码路径拆分新增开销；峰值和 10 次压力为当前构建实测。

## 结论摘要

- 空闲：**42.1 MB WS / 29.0 MB Private / 318 handles / 11 threads / GDI 38**。
- 单次触发峰值：**142.6 MB WS / 97.5 MB Private / 331 handles / 14 threads / GDI 136**，约 1.2–1.5 秒。
- 结束后 3s：**75.2 MB WS / 29.9 MB Private / 329 handles / 10 threads / GDI 38**。
- 10 次连续触发：handles 322→321，GDI 回到 38，Private 29.7 MB，**无累积泄漏**；线程池会短暂升到 14，随后回落到 7。
- 本轮优化把 **GDI 峰值 208→136（-35%）**、**CPU 峰值 62.5→46.9 ms/100 ms（-25%）**；帧数 26→14；镜头每帧不再分配 Bitmap/byte[]。
- 峰值内存仍约 142 MB，主要由 **34 MB 全屏快照 + 34 MB 整屏 layered 暗层** 构成；这是下一步“只截当前显示器/复用快照”的结构性优化空间。
- 最新后台截图修复了 16 秒 UI 阻塞/触发被吞；代价是一次性线程池 +1 线程/+1 句柄，压力下稳定。

## 1. 基础资源消耗

| 场景 | Working Set | Private | Handles | Threads | GDI | CPU |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 空闲 1s | 42.1 MB | 29.0 MB | 318 | 11 | 38 | 低于 1s 采样分辨率 |
| 触发峰值 | 142.6 MB | 97.5 MB | 331 | 14 | 136 | 46.9 ms / 100 ms |
| 结束后 3s | 75.2 MB | 29.9 MB | 329 | 10 | 38 | 接近 0 |
| 10 次后 3s | 42.9 MB | 29.7 MB | 321 | 7 | 38 | 接近 0 |

- CPU 的 `46.9 ms / 100 ms` 是进程 TotalProcessorTime，约 **0.47 个逻辑核**的短时占用；不是整机满载。
- 峰值 Working Set 包含快照、整屏 layered 暗层表面、GDI/DWM 共享页；Private 更能反映进程提交内存。
- 结束后 WS 仍较高是惰性保留；Private 回到 29–30 MB，不是泄漏。

### 磁盘 I/O

- 触发期间读 **0 B**，写约 **206 B/触发**；关闭时再写约 31 B。
- 正常模式只写 `%TEMP%\FindMyCursor.log`；已加 1 MB 轮转上限和写入锁。
- 不生成截图/临时图片；`FindMyCursor-preview*.png` 只在 `--demo` 调试模式生成。
- `settings.ini` 只在启动读取、托盘菜单修改时写入。

### 与优化前对比

| 指标 | 优化前 | 优化后 | 变化 |
| --- | ---: | ---: | ---: |
| GDI 峰值 | 208 | 136 | -35% |
| CPU 峰值 | 62.5 ms/100ms | 46.9 ms/100ms | -25% |
| 动画帧对象 | 26 帧 | 14 帧 | -46% |
| 镜头每帧 Bitmap/byte[] | 每帧新建 | 复用 | LOH 分配消除 |
| 校准后 TIA 查询 | 6.7 次/秒 | 0 | 跨进程消息消除 |
| 峰值内存 | 144.3 MB / 98.6 MB | 142.6 MB / 97.5 MB | 基本不变 |

峰值内存基本不变，因为全屏快照和整屏暗层仍是 O(屏幕面积)；优化主要降低了 CPU、GDI 和 GC/LOH churn。

## 2. 长时间运行稳定性

### 10 次连续触发

- Handles：322 → 321，稳定；
- GDI：峰值 136，结束后 38；
- Private：效果中 95–99 MB，结束后 29.7 MB；
- Threads：峰值 14，结束后 7；
- **未发现句柄/GDI/Private 累积增长。**

### 仍存在的风险

1. **后台截图可能挂起**：`CopyFromScreen` 遇到全屏/独占窗口或显示驱动异常时可能长时间不返回。现在它在后台线程，UI/恢复定时器不受影响；`_captureInProgress` 阻止并发，最多一个挂起线程 + 34 MB，不会每次触发累积。
2. **`BuildFrames` 异常路径**：已加 try/catch 释放部分帧；仍建议在极端 GDI+ 失败时记录日志。
3. **`_triggerEvent` 未 Dispose**：进程生命周期一个句柄，影响很小。
4. **日志轮转**：已加 1 MB 上限；写入锁也覆盖后台截图线程的日志。
5. **ShakeDetector**：已改环形缓冲 + 8ms/2px 降采样；高 polling 下低层钩子耗时从 O(n)（n 可达 6000）降到约 O(94)。
6. **TIA**：校准后不再每 150ms 发跨进程消息；需要时用托盘“输入法颜色”重新校准。
7. **死锁**：所有 UI/定时器/`CursorScaler` 锁都在 UI 线程；截图线程只通过 `BeginInvoke` 回 UI；未发现死锁。主要风险是 `SendMessageTimeout` 120ms 和 `CopyFromScreen` 挂起，属于阻塞而非死锁。

## 3. 极限场景抗压

| 场景 | 资源变化 | 风险 |
| --- | --- | --- |
| 8K 单屏 7680×4320 | 快照约 132 MB + 整屏暗层约 132 MB + 帧/LOH | 300 MB+；32 位/低内存可能 OOM |
| 双 8K/多屏 | 快照/暗层面积线性增长 | 可能 500 MB–1 GB |
| `scale=8`（260px） | 14 帧 Bitmap/HICON 约 20 MB；构建 CPU | 峰值 +20 MB，触发延迟增加 |
| 高频连续触发 | 每次 34 MB 快照 + 暗层表面 | LOH/GC、WS 高水位、周期性卡顿 |
| 8000Hz 鼠标 | 降采样后约 94 样本 | 已从 O(6000) 降到 O(94)，低层钩子压力大幅下降 |
| 截图挂起 | 一个后台线程 + 34 MB | 聚光灯可能不出现，但放大/恢复仍工作 |
| 长期运行 | 日志 1 MB 轮转 | 磁盘增长受控 |

**结论**：中等屏幕、正常触发频率下不会 OOM；极限风险仍主要来自**屏幕面积线性增长的全屏快照/暗层**。如需进一步压峰值，优先“只截当前显示器”或复用快照缓冲。

## 4. 性能瓶颈定位

| 排名 | 代码位置 | 复杂度 | 现状 |
| --- | --- | --- | --- |
| 1 | `Program.ShowSpotlight` 后台 `CopyFromScreen` + 34 MB Bitmap | O(W×H) 时间/空间 | 仍是峰值最大单笔分配 |
| 2 | `SpotlightDimOverlay` 整屏 layered 暗层 | O(W×H) 合成表面 | 结构性问题，难以完全去掉 |
| 3 | `RippleOverlay.Render` + `ApplyCircularMask` + `Push` | O(L²) × 30fps | Bitmap/byte[] 已复用；仍有 `GetHbitmap` GDI 分配 |
| 4 | `CursorScaler` 帧构建/动画 | O(F×H²)，F=14 | 已反向复用 shrink 帧；构建 CPU 降低 |
| 5 | `ShakeDetector.Feed/IsShake` | O(N) 每事件 | 已环形缓冲 + 降采样 |
| 6 | `ImeIndicator.GetState` | O(1) 跨进程 | 校准后停止 |
| 7 | `Log.Write` | O(1) 文件打开/关闭 | 已加锁 + 1 MB 轮转 |

## 5. 已落地优化（优化前后）

### 5.1 镜头 Bitmap / 像素缓冲复用（已落地）

**前**：

```csharp
using (Bitmap bmp = new Bitmap(_size, _size, PixelFormat.Format32bppArgb))
using (Graphics g = Graphics.FromImage(bmp))
{
    ...
    ApplyCircularMask(bmp);   // new byte[stride * height]
    ...
    Push(bmp);                // GetHbitmap()
}
```

**后**：

```csharp
private readonly Bitmap _lens;
private readonly Graphics _lensGraphics;
private readonly byte[] _lensPixels;
private readonly int _lensStride;

// 构造函数只分配一次
_lens = new Bitmap(_size, _size, PixelFormat.Format32bppArgb);
_lensGraphics = Graphics.FromImage(_lens);
BitmapData data = _lens.LockBits(..., ImageLockMode.ReadOnly, ...);
_lensStride = data.Stride;
_lensPixels = new byte[_lensStride * _size];
_lens.UnlockBits(data);

// 每帧
Graphics g = _lensGraphics;
g.Clear(Color.Transparent);
...
ApplyCircularMask(_lens, _lensPixels);
...
Push(_lens);
```

效果：GDI 峰值 208→136，CPU 峰值约 -25%，不再每帧分配 0.6 MB Bitmap + 0.6 MB byte[]。

### 5.2 grow 帧反向复用（已落地）

**前**：grow 14 帧 + shrink 12 帧，共 26 帧、52 个 HICON。

**后**：

```csharp
grow = BuildFrames(startHeight, targetHeight, 14, fillColor, custom, false);
var shrink = new List<CursorFrame>(grow);
shrink.Reverse();
_ownedFrames = grow;
_shrinkFrames = shrink;
```

效果：14 帧、28 个 HICON；帧内存和构建 CPU 约减半；缩回仍然平滑。

### 5.3 ShakeDetector 环形缓冲 + 降采样（已落地）

**前**：`List.RemoveAt(0)` O(n) + 每事件 O(n) 判定。

**后**：

```csharp
if (now - _lastFeed < 8 &&
    Math.Abs(x - _lastX) < 2 && Math.Abs(y - _lastY) < 2) return false;

int tail = (_head + _count) % _ring.Length;
_ring[tail] = new Sample { X = x, Y = y, T = now };
if (_count < _ring.Length) _count++;
else _head = (_head + 1) % _ring.Length;

while (_count > 0 && now - _ring[_head].T > WindowMs) { ... }
```

效果：高 polling 下仍保持约 125Hz 判定；低层鼠标钩子回调显著变短。合成左右摇动实测能正常触发。

### 5.4 校准后停止 TIA 查询（已落地）

**前**：`TryReadGlobalIme` 在 `&&` 左侧，每 150ms 都执行。

**后**：

```csharp
if (!_englishInitialized)
{
    bool native;
    if (TryReadGlobalIme(out native))
    {
        LocalEnglish = !native;
        _englishInitialized = true;
        Log.Write(...);
    }
}
return LocalEnglish ? 0 : 1;
```

效果：校准后不再每 150ms 发跨进程 `SendMessageTimeout`，去掉潜在 120ms UI 阻塞。

### 5.5 其他已落地

- `BuildFrames` try/catch 释放部分帧；
- `Log.Write` 加锁 + 1 MB 轮转（后台截图线程也会写日志）；
- 后台截图 + `_spotlightGeneration` 取消过期结果 + `_captureInProgress` 防并发；
- stale `_active` 恢复：光标已恢复正常但状态未清时，下一次触发自动重置重放；
- 镜头圆孔 **alpha mask 预计算** + 圆环 Pen 复用：每帧不再计算 150k 像素的距离/开方，也不再新建 Pen。实测粗粒度 CPU/GDI 与上一版持平，说明剩余瓶颈主要在整屏截图、`GetHbitmap` 和整屏暗层，而不是遮罩计算。

## 6. 剩余优化空间

1. **只截当前显示器**：快照 34 MB → 约 15 MB，峰值约降 20 MB；跨显示器移动时需要重新截取，或接受跨屏时聚光灯结束。
2. **复用快照缓冲**：避免每次触发 34 MB LOH 分配；代价是常驻增加一块快照，可加 30–60 秒空闲 trim。
3. **DIB/DC 复用**：镜头 `GetHbitmap/DeleteObject` 每帧仍有 GDI 分配；用 `CreateDIBSection` + `CreateCompatibleDC` 只创建一次，可进一步降 GDI/CPU。
4. **`_triggerEvent.Dispose`**：进程退出时补一个句柄释放。
5. **多显示器 DPI**：镜头半径目前取主屏 DPI；不同缩放的多屏上大小不一致，建议 `GetDpiForMonitor`。
6. **截图线程超时兜底**：`_captureInProgress` 已防并发；可记录开始时间，超过 5 秒允许一次新截图并丢弃旧结果。

## 最终判断

当前优化版在 10 次连续触发下稳定，无累积泄漏；相对优化前，GDI 峰值 -35%、CPU 峰值 -25%、帧对象 -46%，镜头 LOH 分配消除，TIA 空闲 IPC 消除。峰值内存仍约 142 MB，是全屏快照 + 整屏暗层的结构性成本；无副作用的 micro-opt（mask 预计算）已落地，但粗测收益很小。如果目标继续压低峰值，下一步只能做有取舍的“只截当前显示器 / 小区域 / Desktop Duplication”。
