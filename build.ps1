# 本地构建（需要 .NET 8 SDK；推荐直接用 GitHub Actions 自动构建）
dotnet publish src/StealthBrowser.csproj -c Release -r win-x64 --self-contained true -o publish
