using Pixora.Models;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace Pixora.Services;

public sealed class ShortcutSettings
{
    private readonly Dictionary<ShortcutAction, List<KeyboardShortcut>> _bindings = [];

    private ShortcutSettings()
    {
    }

    public IReadOnlyDictionary<ShortcutAction, IReadOnlyList<KeyboardShortcut>> Bindings =>
        _bindings.ToDictionary(
            static item => item.Key,
            static item => (IReadOnlyList<KeyboardShortcut>)item.Value);

    public static IReadOnlyList<ShortcutActionInfo> ActionInfos { get; } =
    [
        new(ShortcutAction.OpenImage, "打开图片", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.OpenFolder, "打开目录", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.OpenContainingFolder, "打开文件位置", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.CopyFile, "复制图片文件", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.CopyPath, "复制文件路径", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.CopyName, "复制文件名", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.DeleteImage, "删除到回收站", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.BatchDeleteCurrentFolder, "批量删除当前目录媒体", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleFavorite, "加入/取消收藏", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleFavoritesView, "只看收藏/退出收藏", "文件", ShortcutContext.Viewer),
        new(ShortcutAction.SaveVideoCover, "另存视频封面", "文件", ShortcutContext.Viewer),

        new(ShortcutAction.PreviousImage, "上一张", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.NextImage, "下一张", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleAnimationPlayback, "暂停/继续动图", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.RestartAnimation, "重新播放动图", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.RotateLeft, "向左旋转", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.RotateRight, "向右旋转", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.CycleSortMode, "切换排序方式", "浏览", ShortcutContext.Viewer),
        new(ShortcutAction.ZoomIn, "放大", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ZoomOut, "缩小", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ActualSize, "原始大小", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.FitWindow, "适应窗口", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleFitActual, "适应窗口 / 原始大小", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleFullScreen, "全屏 / 退出全屏", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleInfo, "显示/隐藏图片信息", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleThumbnailSidebar, "显示/隐藏缩略图栏", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ToggleThumbnailColumns, "缩略图单列/双列", "查看", ShortcutContext.Viewer),
        new(ShortcutAction.ShowQuickSearch, "打开快速搜索", "查看", ShortcutContext.Viewer),

        new(ShortcutAction.CropImage, "裁剪图片", "编辑", ShortcutContext.Viewer),
        new(ShortcutAction.CircleCropImage, "圆形裁剪", "编辑", ShortcutContext.Viewer),
        new(ShortcutAction.CompressImage, "压缩图片", "编辑", ShortcutContext.Viewer),
        new(ShortcutAction.OpenBatchCompressTools, "批量压缩图片", "编辑", ShortcutContext.Viewer),
        new(ShortcutAction.SaveCrop, "保存裁剪", "裁剪", ShortcutContext.Crop),

        new(ShortcutAction.CancelOrClose, "取消操作 / 关闭窗口", "窗口", ShortcutContext.Global),
        new(ShortcutAction.ShowShortcutSettings, "打开设置", "窗口", ShortcutContext.Viewer),
    ];

    public static IReadOnlyDictionary<ShortcutAction, string> ActionNames { get; } =
        ActionInfos.ToDictionary(static item => item.Action, static item => item.Name);

    public static ShortcutSettings Load()
    {
        var settings = CreateDefault();
        var path = SettingsPath;
        if (!File.Exists(path))
        {
            return settings;
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<ShortcutSettingsData>(json);
            if (data?.Bindings is null)
            {
                return settings;
            }

            foreach (var item in data.Bindings)
            {
                if (!Enum.TryParse<ShortcutAction>(item.Action, out var action))
                {
                    continue;
                }

                var shortcuts = item.Shortcuts
                    .Select(ToShortcut)
                    .Where(KeyboardShortcut.IsValidInput)
                    .Distinct()
                    .ToList();

                settings._bindings[action] = shortcuts;
            }
        }
        catch
        {
            return settings;
        }

        return settings;
    }

    public static ShortcutSettings CreateDefault()
    {
        var settings = new ShortcutSettings();
        settings.ResetToDefaults();
        return settings;
    }

    public ShortcutSettings Clone()
    {
        var clone = new ShortcutSettings();
        foreach (var item in _bindings)
        {
            clone._bindings[item.Key] = [.. item.Value];
        }

        return clone;
    }

    public void ReplaceWith(ShortcutSettings source)
    {
        _bindings.Clear();
        foreach (var item in source._bindings)
        {
            _bindings[item.Key] = [.. item.Value];
        }
    }

    public void ResetToDefaults()
    {
        _bindings.Clear();
        foreach (var item in ActionInfos)
        {
            _bindings[item.Action] = [];
        }

        _bindings[ShortcutAction.CancelOrClose] = [new(Key.Escape, ModifierKeys.None)];
        _bindings[ShortcutAction.PreviousImage] =
        [
            new(Key.OemQuestion, ModifierKeys.None),
            new(Key.Divide, ModifierKeys.None),
            new(Key.A, ModifierKeys.None),
            new(Key.Left, ModifierKeys.None),
        ];
        _bindings[ShortcutAction.NextImage] =
        [
            new(Key.D, ModifierKeys.None),
            new(Key.Right, ModifierKeys.None),
        ];
        _bindings[ShortcutAction.DeleteImage] = [new(Key.Delete, ModifierKeys.None)];
        _bindings[ShortcutAction.ToggleFavorite] = [new(Key.F, ModifierKeys.Control)];
        _bindings[ShortcutAction.ToggleFavoritesView] = [new(Key.F, ModifierKeys.Control | ModifierKeys.Shift)];
        _bindings[ShortcutAction.CopyFile] = [new(Key.C, ModifierKeys.Control)];
        _bindings[ShortcutAction.CopyPath] = [new(Key.C, ModifierKeys.Control | ModifierKeys.Shift)];
        _bindings[ShortcutAction.OpenContainingFolder] = [new(Key.Enter, ModifierKeys.Control)];
        _bindings[ShortcutAction.ToggleFullScreen] =
        [
            new(Key.Enter, ModifierKeys.None),
            new(Key.F11, ModifierKeys.None),
        ];
        _bindings[ShortcutAction.ToggleFitActual] = [new(Key.Space, ModifierKeys.None)];
        _bindings[ShortcutAction.ZoomIn] =
        [
            new(Key.Multiply, ModifierKeys.None),
            new(Key.Add, ModifierKeys.None),
            new(Key.OemPlus, ModifierKeys.None),
            new(Key.OemPlus, ModifierKeys.Shift),
            new(Key.D8, ModifierKeys.Shift),
        ];
        _bindings[ShortcutAction.ZoomOut] =
        [
            new(Key.Subtract, ModifierKeys.None),
            new(Key.OemMinus, ModifierKeys.None),
        ];
        _bindings[ShortcutAction.ActualSize] =
        [
            new(Key.D0, ModifierKeys.None),
            new(Key.NumPad0, ModifierKeys.None),
        ];
        _bindings[ShortcutAction.FitWindow] = [new(Key.F, ModifierKeys.None)];
        _bindings[ShortcutAction.RotateLeft] = [new(Key.L, ModifierKeys.Control)];
        _bindings[ShortcutAction.RotateRight] = [new(Key.R, ModifierKeys.Control)];
        _bindings[ShortcutAction.ToggleAnimationPlayback] = [new(Key.P, ModifierKeys.None)];
        _bindings[ShortcutAction.RestartAnimation] = [new(Key.P, ModifierKeys.Shift)];
        _bindings[ShortcutAction.CycleSortMode] = [new(Key.S, ModifierKeys.None)];
        _bindings[ShortcutAction.CropImage] = [new(Key.C, ModifierKeys.None)];
        _bindings[ShortcutAction.CircleCropImage] = [new(Key.C, ModifierKeys.Shift)];
        _bindings[ShortcutAction.SaveCrop] = [new(Key.Enter, ModifierKeys.None)];
        _bindings[ShortcutAction.CompressImage] = [new(Key.M, ModifierKeys.Control)];
        _bindings[ShortcutAction.OpenBatchCompressTools] = [new(Key.M, ModifierKeys.Control | ModifierKeys.Shift)];
        _bindings[ShortcutAction.ToggleInfo] = [new(Key.I, ModifierKeys.None)];
        _bindings[ShortcutAction.ToggleThumbnailSidebar] = [new(Key.B, ModifierKeys.Control)];
        _bindings[ShortcutAction.ToggleThumbnailColumns] = [new(Key.B, ModifierKeys.Control | ModifierKeys.Shift)];
        _bindings[ShortcutAction.ShowQuickSearch] = [new(Key.K, ModifierKeys.Control)];
        _bindings[ShortcutAction.OpenImage] = [new(Key.O, ModifierKeys.None)];
        _bindings[ShortcutAction.OpenFolder] = [new(Key.O, ModifierKeys.Control)];
        _bindings[ShortcutAction.ShowShortcutSettings] = [new(Key.OemComma, ModifierKeys.Control)];
    }

    public void ClearAll()
    {
        _bindings.Clear();
        foreach (var item in ActionInfos)
        {
            _bindings[item.Action] = [];
        }
    }

    public bool Matches(ShortcutAction action, KeyboardShortcut shortcut)
    {
        return _bindings.TryGetValue(action, out var shortcuts)
            && shortcuts.Any(item => item.Matches(shortcut.Key, shortcut.Modifiers));
    }

    public IReadOnlyList<KeyboardShortcut> GetShortcuts(ShortcutAction action)
    {
        return _bindings.TryGetValue(action, out var shortcuts) ? shortcuts : [];
    }

    public void AddShortcut(ShortcutAction action, KeyboardShortcut shortcut)
    {
        if (!_bindings.TryGetValue(action, out var shortcuts))
        {
            shortcuts = [];
            _bindings[action] = shortcuts;
        }

        if (!shortcuts.Any(existing => existing.Matches(shortcut.Key, shortcut.Modifiers)))
        {
            shortcuts.Add(shortcut);
        }
    }

    public void ReplaceShortcut(ShortcutAction action, KeyboardShortcut? oldShortcut, KeyboardShortcut newShortcut)
    {
        if (oldShortcut is null)
        {
            AddShortcut(action, newShortcut);
            return;
        }

        if (!_bindings.TryGetValue(action, out var shortcuts))
        {
            _bindings[action] = [newShortcut];
            return;
        }

        var index = shortcuts.FindIndex(existing => existing.Matches(oldShortcut.Key, oldShortcut.Modifiers));
        if (index >= 0)
        {
            shortcuts[index] = newShortcut;
        }
        else
        {
            shortcuts.Add(newShortcut);
        }

        RemoveDuplicateShortcuts(shortcuts);
    }

    public bool RemoveShortcut(ShortcutAction action, KeyboardShortcut shortcut)
    {
        if (!_bindings.TryGetValue(action, out var shortcuts))
        {
            return false;
        }

        var removed = shortcuts.RemoveAll(existing => existing.Matches(shortcut.Key, shortcut.Modifiers)) > 0;
        _bindings[action] = shortcuts;
        return removed;
    }

    public void SetSingleShortcut(ShortcutAction action, KeyboardShortcut shortcut)
    {
        _bindings[action] = [shortcut];
    }

    public ShortcutAction? FindConflict(ShortcutAction action, KeyboardShortcut shortcut)
    {
        foreach (var item in _bindings)
        {
            if (item.Key == action || !ActionsCanConflict(action, item.Key))
            {
                continue;
            }

            if (item.Value.Any(existing => existing.Matches(shortcut.Key, shortcut.Modifiers)))
            {
                return item.Key;
            }
        }

        return null;
    }

    public static ShortcutActionInfo GetActionInfo(ShortcutAction action)
    {
        return ActionInfos.FirstOrDefault(item => item.Action == action) ??
            new ShortcutActionInfo(action, action.ToString(), "其他", ShortcutContext.Viewer);
    }

    private static bool ActionsCanConflict(ShortcutAction left, ShortcutAction right)
    {
        var leftContext = GetActionInfo(left).Context;
        var rightContext = GetActionInfo(right).Context;
        return leftContext == ShortcutContext.Global ||
            rightContext == ShortcutContext.Global ||
            leftContext == rightContext;
    }

    public string Format(ShortcutAction action)
    {
        return _bindings.TryGetValue(action, out var shortcuts)
            ? string.Join(" / ", shortcuts.Select(static shortcut => shortcut.ToDisplayText()))
            : string.Empty;
    }

    public void Save()
    {
        var path = SettingsPath;
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var data = new ShortcutSettingsData
        {
            Bindings = _bindings
                .OrderBy(static item => item.Key.ToString(), StringComparer.Ordinal)
                .Select(item => new ShortcutBindingData(
                    item.Key.ToString(),
                    item.Value.Select(ToData).ToList()))
                .ToList(),
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string SettingsPath =>
        Path.Combine(
            AppInfo.LocalDataFolder,
            "shortcuts.json");

    private static KeyboardShortcut ToShortcut(ShortcutData data)
    {
        var key = Enum.TryParse<Key>(data.Key, out var parsedKey) ? parsedKey : Key.None;
        var modifiers = Enum.TryParse<ModifierKeys>(data.Modifiers, out var parsedModifiers)
            ? parsedModifiers
            : ModifierKeys.None;
        return new KeyboardShortcut(key, KeyboardShortcut.NormalizeModifiers(modifiers));
    }

    private static ShortcutData ToData(KeyboardShortcut shortcut)
    {
        return new ShortcutData(shortcut.Key.ToString(), shortcut.Modifiers.ToString());
    }

    private static void RemoveDuplicateShortcuts(List<KeyboardShortcut> shortcuts)
    {
        for (var i = shortcuts.Count - 1; i >= 0; i--)
        {
            if (shortcuts.FindIndex(existing => existing.Matches(shortcuts[i].Key, shortcuts[i].Modifiers)) != i)
            {
                shortcuts.RemoveAt(i);
            }
        }
    }

    private sealed class ShortcutSettingsData
    {
        public List<ShortcutBindingData>? Bindings { get; set; }
    }

    private sealed class ShortcutBindingData
    {
        public ShortcutBindingData()
        {
        }

        public ShortcutBindingData(string action, List<ShortcutData> shortcuts)
        {
            Action = action;
            Shortcuts = shortcuts;
        }

        public string Action { get; set; } = string.Empty;

        public List<ShortcutData> Shortcuts { get; set; } = [];
    }

    private sealed class ShortcutData
    {
        public ShortcutData()
        {
        }

        public ShortcutData(string key, string modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public string Key { get; set; } = string.Empty;

        public string Modifiers { get; set; } = string.Empty;
    }
}

public enum ShortcutContext
{
    Viewer,
    Crop,
    Global,
}

public sealed record ShortcutActionInfo(
    ShortcutAction Action,
    string Name,
    string Category,
    ShortcutContext Context);
