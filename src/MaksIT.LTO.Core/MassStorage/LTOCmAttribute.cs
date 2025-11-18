using System.Text;

namespace MaksIT.LTO.Core.MassStorage
{

    public enum LTOCmAttributeFormat
    {
        Unknown,
        Binary,
        ASCII,
        Text
    }

    public class LTOCmAttribute
    {
        public ushort Address { get; set; }
        public string? Name { get; set; }
        public bool Writable { get; set; }
        public LTOCmAttributeFormat Format { get; set; }
        public int Length { get; set; }
        public byte[]? Value { get; set; }
        
        public string GetValueAsString()
        {
            switch (Format)
            {
                case LTOCmAttributeFormat.Binary:
                    {
                        if (Value == null || Value.Length == 0 || Value.Length > 8)
                            throw new ArgumentException("Byte array must be between 1 and 8 bytes.");

                        byte[] tempbuffer = new byte[Value.Length];
                        Array.Copy(Value, 0, tempbuffer, 0, Value.Length);

                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(tempbuffer);

                        Int64 result = 0;
                        for (int i = 0; i < tempbuffer.Length; i++)
                        {
                            result *= 256;
                            result += (tempbuffer[i]);
                        }

                        return result.ToString(); // + ' ' + BitConverter.ToString(Value);                        
                    }
                case LTOCmAttributeFormat.ASCII:
                    return Encoding.ASCII.GetString(Value).TrimEnd('\0');
                case LTOCmAttributeFormat.Text:
                    return Encoding.UTF8.GetString(Value).TrimEnd('\0');
                case LTOCmAttributeFormat.Unknown:
                    return BitConverter.ToString(Value);
                default:
                    return BitConverter.ToString(Value);
            }
        }
    }

    public static class LTOCmKnownAttributes
    {
        private static readonly List<LTOCmAttribute> KnownAttributes = new()
        {
            new() { Address = 0x0000, Name = "Remaining capacity in partition [MiB]", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0001, Name = "Maximum capacity in partition [MiB]", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0002, Name = "TapeAlert flags", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0003, Name = "Load count", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0004, Name = "MAM space remaining [B]", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0005, Name = "Assigning organization", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 8 },
            new() { Address = 0x0006, Name = "Format density code", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 1 },
            new() { Address = 0x0007, Name = "Initialization count", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 2 },
            new() { Address = 0x0008, Name = "Volume identifier", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 32 },
            new() { Address = 0x0009, Name = "Volume change reference", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 4 },
            new() { Address = 0x020a, Name = "Density vendor/serial number at last load", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 40 },
            new() { Address = 0x020b, Name = "Density vendor/serial number at load-1", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 40 },
            new() { Address = 0x020c, Name = "Density vendor/serial number at load-2", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 40 },
            new() { Address = 0x020d, Name = "Density vendor/serial number at load-3", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 40 },
            new() { Address = 0x0220, Name = "Total MiB written in medium life", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0221, Name = "Total MiB read in medium life", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0222, Name = "Total MiB written in current/last load", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0223, Name = "Total MiB read in current/last load", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0224, Name = "Logical position of first encrypted block", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0225, Name = "Logical position of first unencrypted block after first encrypted block", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0340, Name = "Medium usage history", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 90 },
            new() { Address = 0x0341, Name = "Partition usage history", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 60 },
            new() { Address = 0x0400, Name = "Medium manufacturer", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 8 },
            new() { Address = 0x0401, Name = "Medium serial number", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 32 },
            new() { Address = 0x0402, Name = "Medium length [m]", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 4 },
            new() { Address = 0x0403, Name = "Medium width [0.1 mm]", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 4 },
            new() { Address = 0x0404, Name = "Assigning organization", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 8 },
            new() { Address = 0x0405, Name = "Medium density code", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 1 },
            new() { Address = 0x0406, Name = "Medium manufacture date", Writable = false, Format = LTOCmAttributeFormat.ASCII, Length = 8 },
            new() { Address = 0x0407, Name = "MAM capacity [B]", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 8 },
            new() { Address = 0x0408, Name = "Medium type", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 1 },
            new() { Address = 0x0409, Name = "Medium type information", Writable = false, Format = LTOCmAttributeFormat.Binary, Length = 2 },
            new() { Address = 0x040a, Name = "Numeric medium serial number", Writable = false, Format = LTOCmAttributeFormat.Unknown, Length = -1 },
            new() { Address = 0x0800, Name = "Application vendor", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 8 },
            new() { Address = 0x0801, Name = "Application name", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 32 },
            new() { Address = 0x0802, Name = "Application version", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 8 },
            new() { Address = 0x0803, Name = "User medium text label", Writable = true, Format = LTOCmAttributeFormat.Text, Length = 160 },
            new() { Address = 0x0804, Name = "Date and time last written", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 12 },
            new() { Address = 0x0805, Name = "Text localization identifier", Writable = true, Format = LTOCmAttributeFormat.Binary, Length = 1 },
            new() { Address = 0x0806, Name = "Barcode", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 32 },
            new() { Address = 0x0807, Name = "Owning host textual name", Writable = true, Format = LTOCmAttributeFormat.Text, Length = 80 },
            new() { Address = 0x0808, Name = "Media pool", Writable = true, Format = LTOCmAttributeFormat.Text, Length = 160 },
            new() { Address = 0x0809, Name = "Partition user text label", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 16 },
            new() { Address = 0x080a, Name = "Load/unload at partition", Writable = true, Format = LTOCmAttributeFormat.Binary, Length = 1 },
            new() { Address = 0x080b, Name = "Application format version", Writable = true, Format = LTOCmAttributeFormat.ASCII, Length = 16 },
            new() { Address = 0x080c, Name = "Volume coherency information", Writable = true, Format = LTOCmAttributeFormat.Unknown, Length = -1 },
            new() { Address = 0x0820, Name = "Medium globally unique identifier", Writable = true, Format = LTOCmAttributeFormat.Binary, Length = 36 },
            new() { Address = 0x0821, Name = "Media pool globally unique identifier", Writable = true, Format = LTOCmAttributeFormat.Binary, Length = 36 }
        };

        public static LTOCmAttribute? GetAttributeByAddress(ushort address)
        {
            return KnownAttributes.Find(a => a.Address == address);
        }        
    }
}