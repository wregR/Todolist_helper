# 发布许可、权利与条件审查

日期：2026-11-02。范围仅为 DesktopTodo-Public 发布仓库、实际恢复的 NuGet 依赖和 Windows x64 包。本机在用版没有升级。

## 结论

第三方依赖的许可类型已核对，缺失的随包许可文件已补齐并纳入打包。发现并修复 SQLite CVE-2025-6965；根据作者的授权决定，项目自身采用 MIT，使用 Todolist_helper contributors 集合署名。签名、远端 CI 和未验收平台不能写成已通过。

## 发现与处理

| 事项 | 原状态 | 处理与现状 |
|---|---|---|
| 项目 LICENSE | 缺失 | 已按作者授权选择 MIT；README 中英同步更新，保留第三方许可 |
| 第三方许可与声明 | 原 ZIP 未包含原文 | 增加 THIRD_PARTY_NOTICES.md、licenses 原文及来源/哈希清单，打包时必须存在 |
| SQLite 原生依赖 | SQLitePCLRaw.lib.e_sqlite3 2.1.11，实际 SQLite 3.49.1 | 改为 SQLitePCLRaw.bundle_e_sqlite3 3.0.5 + SQLite 3.53.4 |
| 已知高危漏洞 | NuGet 报告 GHSA-2m69-gcr7-jv3q / CVE-2025-6965 | 已移除受影响包，升级后 NuGet 依赖查询未再报告已知漏洞；不是“绝无漏洞”的保证 |
| 依赖漏洞检查 | NuGetAudit=false | 改为 true，审计所有传递依赖；警告按错误处理 |
| 依赖可复核性 | 无锁文件 | 生成 packages.lock.json；许可清单记录实际版本与文件哈希 |
| 兼容性文案 | 泛称 Windows x64 | 明确 Windows 11 x64 目标和已验收边界 |
| 签名 | 未签名 | 文档如实标注，不提供绕过安全防护的操作 |
| 私人内容 | 私用数据在另一目录 | 打包白名单排除 data、SDK、快捷方式，保留两目录隔离 |

漏洞公告指出 SQLite <3.50.2 的特定聚合处理可导致内存破坏。本项目使用固定 SQL 和参数，不提供任意 SQL 执行入口；本次没有进行漏洞利用验证，也不据此断言不存在攻击路径。[GitHub reviewed advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)

SQLitePCLRaw 3 的上游迁移说明允许更新 bundle 引用；最新 bundle 使用新的 SQLite 原生包。数据库格式与应用表结构在本次修复中未改变。[Upstream migration](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md)

## 依赖许可及义务

| 组件 | 许可与条件 |
|---|---|
| .NET Runtime / Windows Desktop / Microsoft.Data.Sqlite | MIT；保留版权和许可。运行时自身附带的第三方条款也须保留，不能只附一份 MIT 就认为全部覆盖 |
| SQLitePCLRaw 3.0.5 | Apache-2.0；随分发提供许可，保留适用声明；修改第三方文件时说明修改；上游若包含 NOTICE，保留相关内容 |
| SQLite 原生包 3.53.4 | 包内 LICENSE 为 SQLite 公共领域声明；包装元数据版权另行记录 |
| 字体 | 未打包 Noto 或微软雅黑字体文件；未来若内嵌字体需单独审查分发许可 |
| 图标与风铃 WAV | 在本项目开发中生成，未引入第三方图片或录音；这不是对全部代码原创性或所有权的法律证明 |

Apache-2.0 包含有条件的专利许可与终止条款，不自动授予商标使用权。引用依赖名称用于说明来源，不表示上游背书。[Apache-2.0 original](https://www.apache.org/licenses/LICENSE-2.0)

SQLite 说明适用于实际交付的库代码；构建系统中存在的其他脚本许可不能简单混同为 SQLite 库许可。[SQLite copyright](https://www.sqlite.org/copyright.html)

## 项目自身许可

项目采用 MIT，版权声明为 Todolist_helper contributors，不单列个人署名。MIT 允许商业使用、修改和再分发，要求保留版权和许可声明。第三方组件仍按各自条款保留原文与声明。

[MIT original](https://opensource.org/license/mit)。本次许可选择不代表完成商标或专利全球检索。

## 权限、隐私和发布边界

- app.manifest 为 asInvoker，未请求管理员权限。
- 默认设置会写当前用户开机项；命令注册脚本会写当前用户 PATH，已在中英 README 说明。
- 任务、设置、月计划、备份与错误日志保存在本地，不加密；日志可能包含文件路径。
- 未发现应用自身网络调用；构建、NuGet 审计和下载是联网开发操作，不能等同于运行时遥测。
- 公布错误日志、截图和测试数据之前仍需去除个人计划与本机路径；本次不上传任何数据。
- SHA-256 只检查文件一致性，不代替签名、身份验证或安全审计。
- 本次修复与检查仅在发布仓库完成；私用运行版依然使用原依赖，本次没有替换它。

## 复核依据

包的 .nuspec、project.assets.json、packages.lock.json、runtimeconfig.json 与原生 DLL 版本均用于核对。许可证见 licenses/README.md，兼容性见 COMPATIBILITY.md。原始漏洞查询输出保存在被 Git 忽略的 artifacts/audit 内；旧输出与修复后输出分别保留，不把旧高危报告写成最新结果。

GitHub 公开发布前剩余事项：核对已采用的 MIT 与第三方声明、检查需要公开的源码与文档、在真实远端运行 CI、决定签名和测试平台覆盖。不宣称已完成品牌/商标全球检索或无侵权保证。
