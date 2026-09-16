namespace Scheduler.Common;

/// <summary>
/// 应用数据目录：
/// DEBUG 优先使用仓库根下的 doc/data（避免写入 bin 被清理）；
/// 发布环境使用程序目录下的 data。
/// </summary>
public static class AppDataPaths
{
    private static readonly Lazy<string> RootLazy = new(ResolveRoot);

    /// <summary>data 根目录（已确保存在）</summary>
    public static string Root => RootLazy.Value;

    /// <summary>
    /// 获取 data 下的子目录，并确保目录存在。
    /// 例如 GetDirectory("holiday") -> {Root}/holiday
    /// </summary>
    public static string GetDirectory(params string[] relativeParts)
    {
        var path = relativeParts is { Length: > 0 }
            ? Path.Combine(new[] { Root }.Concat(relativeParts).ToArray())
            : Root;

        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>获取 data 下的文件路径（会创建父目录）</summary>
    public static string GetFile(params string[] relativeParts)
    {
        if (relativeParts is not { Length: > 0 })
            throw new ArgumentException("必须指定相对路径。", nameof(relativeParts));

        var file = Path.Combine(new[] { Root }.Concat(relativeParts).ToArray());
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return file;
    }

    /// <summary>解析 doc/data 根目录</summary>
    private static string ResolveRoot()
    {
        if (DebugHelper.IsDebug)
        {
            // 从输出目录向上查找仓库中的 doc/data
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var docData = Path.Combine(dir.FullName, "doc", "data");
                if (Directory.Exists(docData))
                    return docData;
            }
        }

        var releaseRoot = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(releaseRoot);
        return releaseRoot;
    }
}
