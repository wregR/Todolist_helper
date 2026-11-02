# 兼容性记录 / Compatibility record

审查日期：2026-11-02。此记录区分运行时支持、实际验收和未验收范围。

| 范围 | 结论 |
|---|---|
| Windows 11 x64 25H2 / build 26200 | 本机已完成核心及 WPF 界面检查 |
| 其他受支持的 Windows 11 x64 版本 | 发布目标，未逐版本实机验收 |
| Windows 10 | 未实测；不能从 net10.0-windows 推导出所有 Windows 10 均受支持 |
| Windows ARM64 | 没有原生 ARM64 包；x64 仿真未验收 |
| Windows x86 | 当前仅提供 win-x64，不提供 32 位包 |
| macOS / Linux / Windows Server Core | WPF 桌面界面不适用；不作为发布目标 |
| Windows Server 桌面版 | 未验收 |
| 显示缩放 | 100/125/150% 渲染通过；不等于真实更改系统 DPI、跨显示器拖动和远程桌面均已验收 |
| 开机登录、物理睡眠恢复、多显示器拔插 | 有对应实现与逻辑检查；没有主动操作用户电脑进行完整实机验收 |
| 签名 | DesktopTodo.exe 当前为 NotSigned；未进行 SmartScreen 信誉验收 |
| PowerShell | 脚本已在 PowerShell 7 使用；未逐项验证 Windows PowerShell 5.1 |

依据：[Microsoft .NET 10 supported OS versions](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)。支持表会随系统生命周期变化；本项目不能仅因自包含打包而绕过操作系统要求。

## 运行与部署条件

- 包含 .NET Core 10.0.14、Windows Desktop 10.0.14 和 win-x64 原生 SQLite。最终版本以 runtimeconfig 与许可证清单为准。
- 必须保留 app、bin 和 desktop-todo.portable 层级；只复制 EXE 无法运行。
- 根目录需要当前用户写权限，data 不是加密存储。不要放入需管理员才能写入的位置。
- 多个副本以数据目录隔离单实例；发布副本使用同一个 DesktopTodo.Public 开机项，因此多个发布副本不能同时各自注册为默认启动项。最后运行并注册的副本生效。
- 当前私用版使用不同的 DesktopTodo 开机项，本次发布库不修改它。
- todo 命令存在冲突时注册脚本停止；不会覆盖本机在用命令。
- 更新前退出并备份 data，更新包不包含用户数据库。SQLite 升级无需本次更改应用表结构。
- 字体采用已安装字体回退，不分发系统字体；不同机器的字形与排版可能略有差别。
- 提醒依赖应用正在运行，不提供关机唤醒服务；Windows 音量与设备设置影响音频。

## English summary

Verified on Windows 11 x64 25H2 (build 26200). Other supported Windows 11 x64 versions are targets, not individually tested configurations. Windows 10, ARM64 emulation, server desktops, and Windows PowerShell 5.1 have not been verified. WPF rendering checks are not a complete hardware/DPI test matrix. The package is unsigned, requires a writable root, includes its runtime, and stores unencrypted local data.
