# 更新日志 / Changelog

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)：`主版本.次版本.修订号`。

## v1.2.0 — 2026-10-24

### 新增

- **版本与更新面板**：设置窗口新增「版本与更新」卡片，显示当前版本、最近一次检查时间和可用的新版本。
  - 「检查更新」按钮会立即向 GitHub Releases 查询最新版本。
  - 「打开发布页面」按钮直接跳到最新 Release。
- **桌面更新提示**：检测到新版本后，桌面倒计时会短暂淡入一张「发现新版本」提示卡（约 8 秒后淡出），可直接到 Release 页面下载。
- **自动检查**：启动时最多每 12 小时检查一次，结果缓存在本地，离线时不影响倒计时。

### 改进

- **背景分辨率跟随屏幕**：预烘焙背景现在按当前显示器物理分辨率生成（例如 2560×1600），不再固定为 1920×1200。
- **画质提升**：背景 JPEG 质量由 92 提升到 96；模糊半径随分辨率等比缩放，视觉柔和度保持一致。
- **设置窗口加高**，容纳新的更新卡片。

### 修复

- 修正 `install.ps1` 在 Explorer 占用快捷方式时写入属性失败的问题，增加了重试。
- 修正非 16:10 屏幕下背景主体偏移的问题。

---

## English

### v1.2.0 — 2026-10-24

**Added**

- Version & updates card in Settings: current version, last check time and available release.
- "Check now" and "Open releases" actions.
- Desktop update notice: a toast fades in over the baked background when a newer release is found.
- Automatic check at most once every 12 hours, cached locally and offline-safe.

**Improved**

- The baked background is now rendered at the monitor's physical resolution (e.g. 2560×1600).
- JPEG quality raised to 96; blur radius scales with resolution.
- Taller settings window for the new update card.

**Fixed**

- `install.ps1` now retries writing shortcut properties while Explorer holds the file.
- Background framing on screens narrower than 16:10.
