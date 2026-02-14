自动发布新版本。执行以下步骤：

1. 运行 `git status` 和 `git log --oneline -5` 查看当前状态
2. 根据上次 release tag 自动递增版本号（如 v1.0.3 → v1.0.4）
3. 将所有改动文件 `git add` 并 `git commit`，commit message 根据 diff 内容自动生成
4. `git push origin main`
5. 编译两个版本：
   - 轻量版（框架依赖）：`dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true`
   - 完整版（自包含）：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
6. 用 powershell Compress-Archive 分别打包：
   - `FancyStart-{版本号}-win-x64-lite.zip`（轻量版，几百KB）
   - `FancyStart-{版本号}-win-x64.zip`（完整版，~60MB）
7. 用 `"/c/Program Files/GitHub CLI/gh.exe" release create {版本号} {lite-zip} {full-zip} --title "{版本号}" --notes "{release notes}"` 发布到 GitHub
8. release notes 根据自上个版本以来的 commit 内容自动生成，使用中文，末尾注明：
   - lite 版需安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
   - 完整版开箱即用

注意事项：
- 如果 FancyStart.exe 被占用导致编译失败，提示用户关闭后重试
- gh.exe 的完整路径是 `/c/Program Files/GitHub CLI/gh.exe`
- 上传可能因网络问题失败，失败后自动重试一次
- 两次 publish 输出目录相同，先编译轻量版并立即打包，再编译完整版并打包
