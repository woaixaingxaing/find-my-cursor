# 找找鼠标 · Find My Cursor

模仿 macOS「摇一摇鼠标，快速找到指针」的交互设计。对应优设文章
[《为什么苹果的动效设计每一帧都让人爱不释手（下篇）》](https://www.uisdc.com/apple-dynamic-effect-design-2)
里的「快速定位」一节。

同一套交互做了两个版本：

| 版本 | 位置 | 特点 |
| --- | --- | --- |
| 网页 Demo | `index.html` | 打开即玩，方便调参数、看动效 |
| Windows 桌面版 | `windows/FindMyCursor.exe` | 全局生效，真实放大系统指针并叠加光环 |

---

## 一、网页 Demo

直接双击 `index.html`，或拖进浏览器。

- 在页面里**快速左右摇动鼠标**：0.75 秒内至少 4 次方向反转、横向位移超过 100px。
- 或者按 `Space` / 点击底部提示，直接预览。
- 指针放大 2.4 倍，弹出三层声呐光环，1.35 秒后自动缩回。
- 页面里的指针是自定义 SVG 指针，隐藏了系统默认指针。

实现要点：

- `pointermove` 采样最近 750ms 的坐标，统计 X / Y 轴方向反转次数；
- `transform: scale()` + 带过冲的 `cubic-bezier` 做弹性放大；
- 三道 `@keyframes` 光环错开 110ms 扩散，`prefers-reduced-motion` 下自动收敛；
- 全部样式、图标、壁纸都是内联的，没有外部依赖。

### 想调手感

在 `index.html` 里改这几个值：

| 变量 / 函数 | 作用 |
| --- | --- |
| `--zoom` | 放大倍数，默认 `2.4` |
| `track()` 里的 `750` | 摇动判定窗口（毫秒） |
| `isShake()` 里的 `revX >= 4` | 需要几次方向反转 |
| `isShake()` 里的 `spanX > 100` | 横向最小位移 |
| `fire()` 里的 `1800` / `1350` | 冷却时间 / 保持时间 |

---

## 二、Windows 桌面版

双击 `FindMyCursor.exe` 即可，无需安装（依赖系统自带的 .NET Framework 4.x）。
可执行文件在 [Releases](https://github.com/woaixaingxaing/find-my-cursor/releases/latest) 下载；
源码在 `windows/` 下，运行 `windows/build.ps1` 自行编译。

启动后在后台常驻，默认不显示主窗口、任务栏按钮或托盘图标。在**任何界面**快速左右摇动鼠标：

1. 使用自定义的**无黑边渐变箭头**（96 / 128 / 160 三种尺寸同一套设计），颜色跟随当前输入法状态：
   **红 = 中文、蓝 = 英文、绿 = Caps Lock 大写**；箭头和输入框里的 I-beam 会一起放大；
2. 约 **0.28 秒**平滑放大到目标尺寸，不再是瞬间跳变；
3. 同时出现 **PowerToys Find My Mouse 风格**的聚光灯：所有显示器一起压暗，
   鼠标处用一个小镜头重绘压暗前的屏幕内容，所以中间保持原样、不填白色；
   圆斑固定 100px 并跟随鼠标，边缘 5px 柔化；
4. 约 1.2 秒后用 **0.24 秒**平滑缩回，并**原样恢复你放大前的指针**——会保存指针句柄，
   不会换成系统默认箭头。

启动应用、再次运行应用、按 `Ctrl+Alt+F` 或**双击右 Shift**，都会立即播放动画；摇动鼠标也会触发。

### 托盘菜单（可选）

默认**不显示托盘图标和任务栏按钮**，只在后台常驻。需要菜单时，把 `settings.ini`
里的 `tray` 改成 `1` 并重启；菜单包含：

- **立即放大指针**：手动触发一次，方便确认效果
- **指针大小**：标准 96 px / 大 128 px / 超大 160 px
- **指针颜色**：跟随当前指针 / 使用配置文件颜色；可打开配置文件
- **指针风格**：自定义（推荐，红渐变箭头）/ 跟随当前指针
- **输入法指示**：红=中文 / 蓝=英文 / 绿=Caps Lock（内置状态机）
- **启用摇动检测**：临时关闭/恢复全局监听
- **显示聚光灯**：打开/关闭 PowerToys 风格的全屏压暗 + 鼠标处透明洞
- **放大光标**：取消勾选 = 纯 PowerToys 聚光灯，光标保持原大小
- **退出**

快捷键：`Ctrl + Alt + F`，或**快速双击右 Shift**。没有托盘时，用 `FindMyCursor.exe --exit`
退出后台实例（也可以写一个快捷方式指向这条命令）。

### 配置文件

路径：`FindMyCursor.exe` 同目录下的 `settings.ini`（首次启动自动生成）。
安装版位于：`%LOCALAPPDATA%\InputCursorIndicator\settings.ini`
（旧版工具也留在同一目录，便于回滚）。

```ini
# auto = 跟随当前指针；fixed = 使用 fill/outline
scale=4.0
enabled=1
enlarge=1
tray=0
colorMode=auto
fill=#E63741
outline=#2D1216
ripple=1
cursorStyle=custom
imeIndicator=1
imeSize=28
```

| 键 | 说明 |
| --- | --- |
| `scale` | 3 / 4 / 5 → 96 / 128 / 160 px |
| `enabled` | 1 开启摇动检测，0 暂停 |
| `enlarge` | 1 放大光标；0 只显示聚光灯，光标保持原大小 |
| `tray` | 1 显示托盘图标和菜单；0（默认）完全隐藏，只保留热键和摇动 |
| `colorMode` | `auto` 采样当前指针颜色；`fixed` 使用下面的颜色 |
| `fill` / `outline` | 填充色 / 描边色，`#RRGGBB` |
| `ripple` | 1 显示 PowerToys 风格聚光灯，0 关闭 |
| `cursorStyle` | `custom` 使用 `fill` 的无描边红渐变箭头；`live` 跟随当前指针 |
| `imeIndicator` | 1 开启输入法指示（打字时也用同一套红/蓝/绿指针），0 关闭 |
| `imeSize` | 打字时正常指针的可见高度，默认 28 px |

修改 `fill` / `outline` 后重启 FindMyCursor 生效。`cursorStyle=custom` 只使用
`fill`（无描边）；`cursorStyle=live` 时按 `colorMode`（auto/fixed）决定颜色，
`outline` 只在 live 模式使用。开启托盘（`tray=1`）后，菜单里的「指针风格」「指针颜色」「显示聚光灯」都可以即时切换。

> 输入法状态显示已由 FindMyCursor 内置接管（`imeIndicator=1`），不再需要原来的
> `输入法彩色鼠标指针.exe`（它的启动项已移除，exe 保留在安装目录只用于回滚）。
> 内置状态机按 **红 = 中文 / 蓝 = 英文 / 绿 = Caps Lock 大写** 给两种光标同时上色：
> 普通箭头（`OCR_NORMAL`）和输入框里的文本 I-beam（`OCR_IBEAM`），都是无描边设计。
> 中/英基线由 Windows 11 输入指示器的全局转换模式校准；之后**单击左 Shift** 或
> `Ctrl+Space` 切换英文，Shift 期间若夹带鼠标点击/其他按键则不会误触发。
> **右 Shift 专用于双击触发**，单击它不会再切换中英文，避免两个手势互相干扰。
> Caps Lock → 绿。Chrome/Electron 的窗口级 IMM 状态不可靠，不再作为依据，
> 更不会向系统发送任何 `IMC_SET*` 命令去改你的输入法。
> 放大指针在 `colorMode=auto` 下会采样当前活动指针的颜色，放大版与正常指针同色；
> `colorMode=fixed` 才固定用配置里的 `fill`。

### 安全兜底

程序正常退出、关闭检测、放大结束都会自动恢复系统指针。

如果程序在放大状态下被强制结束（任务管理器结束进程、崩溃等），系统指针会停留在放大状态。此时运行：

```powershell
FindMyCursor.exe --restore
```

即可恢复。也可以重新应用一次鼠标指针方案，或重启系统。

> 程序使用了全局鼠标钩子（`WH_MOUSE_LL`）和 `SetSystemCursor`，
> 个别安全软件可能会提示；源码全部在 `windows/` 下，可自行审查、编译。

### 重新编译

需要 Windows 自带的 .NET Framework 4.x 编译器：

```powershell
powershell -ExecutionPolicy Bypass -File windows\build.ps1
```

输出：`windows\FindMyCursor.exe`。

### 开机静默启动（已配置）

安装到当前用户目录 `%LOCALAPPDATA%\InputCursorIndicator\`，并在
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 注册了唯一一个启动项：

```text
FindMyCursor = "...\InputCursorIndicator\FindMyCursor.exe" --silent
```

旧版 `输入法彩色鼠标指针.exe` 的启动项已移除，进程也已停止；它的 exe 仍留在同目录，
需要回滚时手动启动即可。登录后 FindMyCursor 静默常驻后台（不显示窗口、任务栏按钮和托盘图标）；`--silent` 不播放启动动画，
手动运行或按 `Ctrl+Alt+F` 仍会触发。需要退出时运行 `FindMyCursor.exe --exit`。

> **不要把它们安装成 Windows 服务**。服务运行在 Session 0，无法访问交互桌面，
> 既不能设置当前用户的光标，也不能接收全局鼠标钩子；真正的“服务”会直接失效。
> 任务管理器「启动」里的启动项（Run 键）才是这类桌面工具的正确自启方式。

### 实现结构

```
windows/
├─ Program.cs          入口、托盘菜单、触发流程、单实例
├─ Interop.cs           Win32 声明、鼠标钩子、全局热键、指针缩放/图标生成
├─ CursorCapture.cs     抓取当前系统箭头（用于采样颜色）
├─ CursorVector.cs      颜色采样与轮廓工具
├─ ImeIndicator.cs      输入法三色状态机（红/蓝/绿）与系统指针应用
├─ KeyboardHook.cs      双击右 Shift、左 Shift 轻按、Ctrl+Space、Caps Lock
├─ RippleOverlay.cs     跟随鼠标的小镜头（重绘压暗前快照，抗锯齿圆边）
├─ SpotlightDimOverlay.cs 静态整屏暗层（无窗口区域，避免每帧重挖洞）
├─ Settings.cs          配置（大小 / 颜色 / 风格 / 聚光灯）
├─ ShakeDetector.cs     摇动识别（1200ms 窗口 + 速度上限）
├─ AssemblyInfo.cs      文件描述（通知里显示「找找鼠标」）
└─ build.ps1            编译脚本
```

几个关键点：

- **摇动识别**：低级鼠标钩子只记录坐标，不拦截事件；最近 1200ms 内统计方向反转。
  需要 6 次以上反转、位移超过 120px、累计路径超过 400px，且首次到最后一次反转
  持续至少 350ms；反转间隔小于 60ms 的高频抖动会被过滤，采样率上限约 125 Hz。
- **指针放大**：逐帧生成 28→128 px 的箭头和 I-beam（I-beam 也一起放大），
  用 `CreateIconIndirect` + `SetSystemCursor(OCR_NORMAL/OCR_IBEAM)` 替换系统指针；
  280ms 缓出放大，保持约 0.9 秒，240ms 缓入缩回。`enlarge=0` 时跳过放大。
- **聚光灯**：整个虚拟屏幕先做一层静态暗层（没有窗口区域，不再每帧重挖洞）；
  鼠标处用一个小镜头窗口重绘“压暗前”的屏幕快照，圆形边缘用 5px 的 alpha 渐变
  做抗锯齿，并跟随鼠标移动。这样中间保持原样、边缘平滑，且每帧只更新一个小位图。
  镜头用的是触发瞬间的快照，静态页面完全一致；视频里会显示一帧静止画面。
- **原样恢复**：放大前用 `CopyIcon` 保存当前箭头和 I-beam 句柄；缩回后
  `SetSystemCursor` 放回。因此缩小后不会变成系统默认箭头，而是你放大前的那一枚。
- **直接播放**：启动、二次启动（通知常驻实例）、`Ctrl+Alt+F`、双击右 Shift 都走同一个触发入口。
- **防误触**：2.5 秒冷却；6 次以上方向反转、反转间隔 ≥60ms、速率 ≤15 次/秒、
  持续 ≥350ms，快速抖动或正常移动/滚动都不会触发。
- **触发后保色**：放大结束时会重新应用当前输入法的红/蓝/绿指针，避免恢复系统指针后掉色。

### 已知问题：打开任务管理器时鼠标短暂变快/变慢

实测环境：G502 HERO + NVIDIA GT 710 + 两个 1080p 虚拟显示器。

- 任务管理器加载阶段 CPU 峰值约 9%，DPC < 1%，磁盘几乎为 0；
- 鼠标输入出现 50–119ms 间隔，加载完成后恢复；
- 完全退出 G HUB 后仍能复现。

因此这不是 FindMyCursor 或鼠标 DPI 的问题，而是任务管理器空白加载阶段的窗口绘制
加上 GT 710 的 DWM 合成瓶颈；ToDesk / GameViewer 等虚拟显示器会加重。

建议：禁用不用的虚拟显示器；任务管理器改用“详细信息”标签或 Process Explorer；
减少同时启用的显示器、关闭透明效果；长期可换 GT 1030 / GTX 1650 以上显卡。

---

## 三、这个设计为什么成立

对应文章里的三个点：

1. **顺应本能**：找不到指针时，人会下意识地晃动鼠标或视线，交互只是接住了这个动作。
2. **强反馈、低打扰**：不弹窗、不改变布局，指针原地放大，视线被温和地拉回去。
3. **自动退场**：找到即恢复。高频操作里，克制比炫技更重要。

网页版用来推敲视觉与手感，Windows 版把它变成每天都能用的工具。
