自动发布新版本。执行以下步骤：

1. 运行 `git status` 和 `git log --oneline -5` 查看当前状态
2. 根据上次 release tag 自动递增版本号（如 v1.0.3 → v1.0.4）
3. 将所有改动文件 `git add` 并 `git commit`，commit message 根据 diff 内容自动生成
4. `git push origin main`
5. 编译两个版本：
   - 精简版（需要 .NET 8 运行时）：`dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained`
   - 独立版（含运行时，无需安装 .NET）：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
6. 用 powershell Compress-Archive 分别打包：
   - `FancyStart-{版本号}-win-x64-lite.zip`（精简版）
   - `FancyStart-{版本号}-win-x64.zip`（独立版）
   两个 zip 的源文件路径相同：`bin\Release\net8.0-windows\win-x64\publish\FancyStart.exe`（每次 publish 会覆盖）
7. 用 `"/c/Program Files/GitHub CLI/gh.exe" release create {版本号} {lite.zip} {full.zip} --title "{版本号}" --notes "{release notes}"` 发布到 GitHub
8. release notes 根据自上个版本以来的 commit 内容自动生成，使用中文，末尾附上说明：`lite 版需要安装 .NET 8 桌面运行时，独立版可直接运行`

9. 发布成功后删除本次生成的两个 zip 文件

注意事项：
- 如果 FancyStart.exe 被占用导致编译失败，提示用户关闭后重试
- gh.exe 的完整路径是 `/c/Program Files/GitHub CLI/gh.exe`
- 上传可能因网络问题失败，失败后自动重试一次
- 先编译精简版再编译独立版，因为两者输出路径相同，需要分别打包
