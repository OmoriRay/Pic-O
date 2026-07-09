# PureView 批量压缩脚本说明

PureView 右键菜单里的“批量压缩图片”已经提供可视化批量压缩窗口。这个脚本保留为备用方案，适合在命令行里批处理或排查问题时使用：

```powershell
.\batch-compress.ps1
```

## 先安装 ImageMagick

这个脚本调用 ImageMagick 的 `magick` 命令。先安装 ImageMagick，并确保 PowerShell 里能运行：

```powershell
magick -version
```

如果提示找不到 `magick`，说明 ImageMagick 没装好，或者没有加入系统 PATH。

## 最常用命令

压缩某个文件夹里的图片，输出到该文件夹里的 `compressed` 子目录：

```powershell
.\batch-compress.ps1 -InputPath "D:\Pictures" -Quality 82 -MaxWidth 1920 -MaxHeight 1920 -Format jpg
```

压缩单张图片：

```powershell
.\batch-compress.ps1 -InputPath "D:\Pictures\photo.png" -Quality 82 -Format jpg
```

递归压缩文件夹和子文件夹：

```powershell
.\batch-compress.ps1 -InputPath "D:\Pictures" -Recurse -Quality 82 -MaxWidth 1920 -MaxHeight 1920 -Format jpg
```

指定输出目录：

```powershell
.\batch-compress.ps1 -InputPath "D:\Pictures" -OutputFolder "D:\Pictures_compressed" -Quality 82 -Format jpg
```

## 参数说明

- `-InputPath`：必填。可以是图片文件，也可以是文件夹。
- `-OutputFolder`：输出目录。不填时，默认输出到输入目录旁边的 `compressed` 文件夹。
- `-Quality`：压缩质量，1 到 100。JPG/WebP 常用 `75` 到 `88`。
- `-MaxWidth`：最大宽度。填 `1920` 表示宽度超过 1920 才缩小。
- `-MaxHeight`：最大高度。填 `1920` 表示高度超过 1920 才缩小。
- `-Format`：输出格式，可选 `jpg`、`png`、`webp`。
- `-Recurse`：处理子文件夹。

## 注意

- 脚本不会覆盖原图，会写到输出目录。
- 默认支持常见静态图片：JPG、PNG、BMP、WebP、TIFF、HEIC、AVIF 等。
- `-MaxWidth 0 -MaxHeight 0` 表示不改变尺寸，只转换/压缩格式。
- PNG 通常不按 `-Quality` 变小很多；想要明显减小体积，优先用 `jpg` 或 `webp`。
