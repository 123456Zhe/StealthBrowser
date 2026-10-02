# 本地构建（需要 .NET SDK；推荐直接用 GitHub Actions 自动构建）
dotnet msbuild -t:restore StealthBrowser.csproj
dotnet msbuild StealthBrowser.csproj /p:Configuration=Release
