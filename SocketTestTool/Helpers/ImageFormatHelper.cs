namespace SocketTestTool.Helpers;

/// <summary>
/// 依据文件头（魔数）识别常见图片格式，用于在预览前判断字节流是否为图片。
/// </summary>
public static class ImageFormatHelper
{
    /// <summary>
    /// 识别图片格式，返回 PNG / JPEG / GIF / BMP / WEBP / ICO / TIFF；
    /// 若不是已知图片格式则返回 null。
    /// </summary>
    public static string? Detect(byte[] data)
    {
        if (data.Length < 8) return null;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "PNG";

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "JPEG";

        // GIF: "GIF8"
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return "GIF";

        // BMP: "BM"
        if (data[0] == 0x42 && data[1] == 0x4D)
            return "BMP";

        // ICO: 00 00 01 00
        if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
            return "ICO";

        // WEBP: "RIFF" ???? "WEBP"
        if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "WEBP";

        // TIFF: "II*\0" 或 "MM\0*"
        if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||
            (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A))
            return "TIFF";

        return null;
    }
}
