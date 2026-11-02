# GitHub 发布准备

## 本地审查

仓库根目录为 DesktopTodo-Public。基线与发布准备分别记录为 Git 提交；未提交改动用 git diff 查看，已提交改动用 git show 查看。

```powershell
git status --short
git diff
git diff --stat
```

只提交源码、脚本、文档与应用素材。个人任务数据库、SDK、测试记录、安装目录和快捷方式由 .gitignore 排除。

## 生成发布包

```powershell
./release.ps1
```

输出 artifacts/releases 下的 Windows x64 ZIP 和 SHA-256 校验文件。发布目录以白名单方式从构建结果组装，不包含 data 目录。包中保留 app 和 bin 层级，以及独立的 portable 根目录标记。

## 上传前需要确定

- GitHub 仓库所属账号、名称、公开或私有。
- 项目采用 MIT 与贡献者集合署名；第三方声明与原文已纳入打包，见 RELEASE_AUDIT.md。
- 确认 README 与版本号，审查并提交当前改动。

上传源码后，main 与 PR 会触发构建。推送 v* 标签会生成 GitHub Release 草稿；草稿仍需检查后再公开发布。本地验证不代表 GitHub Actions 已在远端运行成功。

## 与本机在用版本隔离

发布工作区是 DesktopTodo-Public。本机在用版本位于同级 DesktopTodo，禁止将发布构建覆盖到该目录。发布工作区拥有独立的 .git、.tools、artifacts 和数据路径。发布版开机入口使用 DesktopTodo.Public，不覆盖在用版本的 DesktopTodo 项；终端启用脚本遇到同名 todo 命令会停止。
