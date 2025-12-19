# 清理缓存
dotnet clean

# 删除 bin 和 obj
rmdir /s /q src\bin
rmdir /s /q src\obj

# 重新编译
dotnet restore
dotnet build -c Release