namespace SocketTestTool.Helpers;

/// <summary>支持的校验算法</summary>
public enum ChecksumAlgorithm
{
    Crc8,
    Crc8Maxim,
    Crc16Modbus,
    Crc16CcittFalse,
    Crc16Kermit,
    Crc16X25,
    Crc16Usb,
    Crc16Ibm,
    Crc16Maxim,
    Crc32,
    Checksum8,
    Checksum16,
    Checksum32,
    Lrc,
    BccXor,
    Sum
}

/// <summary>
/// 常见校验算法计算：CRC8 / CRC16（多套参数）/ CRC32 / 累加和 / LRC / BCC 异或。
/// 全部为纯函数，输入输出均为字节数组或数值，便于在工具窗口与后续协议解析中复用。
/// </summary>
public static class ChecksumHelper
{
    /// <summary>与 <see cref="ChecksumAlgorithm"/> 顺序一致的显示名称</summary>
    public static string[] AlgorithmNames { get; } =
    [
        "CRC8 (poly 0x07)",
        "CRC8/MAXIM (poly 0x31)",
        "CRC16/MODBUS",
        "CRC16/CCITT-FALSE",
        "CRC16/KERMIT (CCITT-TRUE)",
        "CRC16/X25",
        "CRC16/USB",
        "CRC16/IBM (ARC)",
        "CRC16/MAXIM",
        "CRC32",
        "Checksum8 (累加和低 8 位)",
        "Checksum16 (累加和低 16 位)",
        "Checksum32 (累加和低 32 位)",
        "LRC (纵向冗余校验)",
        "BCC (异或校验)",
        "SUM (字节累加和)"
    ];

    /// <summary>算法结果占用的字节数</summary>
    public static int ByteWidth(ChecksumAlgorithm alg) => alg switch
    {
        ChecksumAlgorithm.Crc8 or ChecksumAlgorithm.Crc8Maxim
            or ChecksumAlgorithm.Checksum8 or ChecksumAlgorithm.Lrc or ChecksumAlgorithm.BccXor => 1,
        ChecksumAlgorithm.Crc16Modbus or ChecksumAlgorithm.Crc16CcittFalse or ChecksumAlgorithm.Crc16Kermit
            or ChecksumAlgorithm.Crc16X25 or ChecksumAlgorithm.Crc16Usb or ChecksumAlgorithm.Crc16Ibm
            or ChecksumAlgorithm.Crc16Maxim or ChecksumAlgorithm.Checksum16 => 2,
        _ => 4
    };

    /// <summary>计算校验值（无符号整数形式）</summary>
    public static ulong ComputeValue(ChecksumAlgorithm alg, byte[] data)
    {
        ulong sum = 0;
        switch (alg)
        {
            case ChecksumAlgorithm.Crc8:
                return Crc(data, 8, 0x07, 0x00, false, false, 0x00);
            case ChecksumAlgorithm.Crc8Maxim:
                return Crc(data, 8, 0x31, 0x00, true, true, 0x00);
            case ChecksumAlgorithm.Crc16Modbus:
                return Crc(data, 16, 0x8005, 0xFFFF, true, true, 0x0000);
            case ChecksumAlgorithm.Crc16CcittFalse:
                return Crc(data, 16, 0x1021, 0xFFFF, false, false, 0x0000);
            case ChecksumAlgorithm.Crc16Kermit:
                return Crc(data, 16, 0x1021, 0x0000, true, true, 0x0000);
            case ChecksumAlgorithm.Crc16X25:
                return Crc(data, 16, 0x1021, 0xFFFF, true, true, 0xFFFF);
            case ChecksumAlgorithm.Crc16Usb:
                return Crc(data, 16, 0x8005, 0xFFFF, true, true, 0xFFFF);
            case ChecksumAlgorithm.Crc16Ibm:
                return Crc(data, 16, 0x8005, 0x0000, true, true, 0x0000);
            case ChecksumAlgorithm.Crc16Maxim:
                return Crc(data, 16, 0x8005, 0x0000, true, true, 0xFFFF);
            case ChecksumAlgorithm.Crc32:
                return Crc(data, 32, 0x04C11DB7, 0xFFFFFFFF, true, true, 0xFFFFFFFF);

            case ChecksumAlgorithm.BccXor:
                ulong xor = 0;
                foreach (byte b in data) xor ^= b;
                return xor;

            case ChecksumAlgorithm.Lrc:
                foreach (byte b in data) sum += b;
                return (ulong)((0x100 - (sum & 0xFF)) & 0xFF);

            case ChecksumAlgorithm.Checksum8:
                foreach (byte b in data) sum += b;
                return sum & 0xFF;
            case ChecksumAlgorithm.Checksum16:
                foreach (byte b in data) sum += b;
                return sum & 0xFFFF;
            case ChecksumAlgorithm.Checksum32:
            case ChecksumAlgorithm.Sum:
                foreach (byte b in data) sum += b;
                return alg == ChecksumAlgorithm.Checksum32 ? sum & 0xFFFFFFFF : sum;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 计算校验值并按指定字节序输出字节。
    /// littleEndian=true 时低字节在前（如 Modbus 报文中的 CRC 排列）。
    /// </summary>
    public static byte[] ComputeBytes(ChecksumAlgorithm alg, byte[] data, bool littleEndian = true)
    {
        ulong value = ComputeValue(alg, data);
        int width = ByteWidth(alg);
        var bytes = new byte[width];
        for (int i = 0; i < width; i++)
        {
            int shift = i * 8;
            byte b = (byte)((value >> shift) & 0xFF);
            bytes[littleEndian ? i : width - 1 - i] = b;
        }
        return bytes;
    }

    /// <summary>计算并以「空格分隔的 HEX」形式返回，如 "3C D5"</summary>
    public static string ComputeHex(ChecksumAlgorithm alg, byte[] data, bool littleEndian = true)
        => HexHelper.ToHex(ComputeBytes(alg, data, littleEndian));

    /// <summary>
    /// 通用位运算 CRC：poly 为正常（非反射）形式的多项式。
    /// </summary>
    private static ulong Crc(byte[] data, int width, ulong poly, ulong init, bool refin, bool refout, ulong xorout)
    {
        ulong mask = width switch { 8 => 0xFFUL, 16 => 0xFFFFUL, _ => 0xFFFFFFFFUL };
        ulong topBit = 1UL << (width - 1);
        ulong crc = init & mask;

        foreach (byte raw in data)
        {
            ulong b = refin ? Reflect(raw, 8) : raw;
            crc ^= b << (width - 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & topBit) != 0 ? (crc << 1) ^ poly : crc << 1;
        }

        crc &= mask;
        if (refout) crc = Reflect(crc, width);
        return (crc ^ xorout) & mask;
    }

    /// <summary>按位反转低 width 位</summary>
    private static ulong Reflect(ulong value, int width)
    {
        ulong result = 0;
        for (int i = 0; i < width; i++)
        {
            result <<= 1;
            result |= value & 1;
            value >>= 1;
        }
        return result;
    }
}
