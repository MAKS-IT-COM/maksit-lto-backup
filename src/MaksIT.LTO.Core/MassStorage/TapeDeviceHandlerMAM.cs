using MaksIT.LTO.Core.MassStorage;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MaksIT.LTO.Core;

public partial class TapeDeviceHandler : IDisposable
{

    #region LTO-CM Structures from IBM Documentation


    public class LTOCartridgeMemory
    {
        private uint MaxBufferSize = 32736; // Maximum buffer size for SCSI commands

        private TapeDeviceHandler parent;

        public List<LTOCmAttribute> Attributes;

        public LTOCartridgeMemory(TapeDeviceHandler parentInstance)
        {
            this.parent = parentInstance;
            Attributes = new List<LTOCmAttribute>();
        }

        public void ReadCartridgeAttributes()
        {
            IntPtr ptrIn = IntPtr.Zero;

            byte[] buffer = new byte[MaxBufferSize];
            GCHandle dataHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            SCSI_PASS_THROUGH_DIRECT spt = new SCSI_PASS_THROUGH_DIRECT();

            spt.DataIn = 1; // Data transfer direction: IN
            spt.TimeOutValue = 60;
            spt.CdbLength = 16; // CDB length for READ ATTRIBUTE command
            spt.Cdb = new byte[spt.CdbLength];
            spt.Cdb[0] = 0x8C; // READ ATTRIBUTE command code
            spt.Cdb[1] = 0x00; // Service Action Sub-command code 
            spt.Cdb[2] = 0x00; // Obsolete
            spt.Cdb[3] = 0x00; // Obsolete
            spt.Cdb[4] = 0x00; // Obsolete
            spt.Cdb[5] = 0x00; // Logical Volume number
            spt.Cdb[6] = 0x00; // Reserved
            spt.Cdb[7] = 0x00; // Partition Number

            spt.Cdb[8] = 0x00; // First attribute identifier (MSB)
            spt.Cdb[9] = 0x00; // First attribute identifier (LSB)  

            // Isnt Allocation Length size, 4 Bytes?
            spt.Cdb[10] = 0x02; // Allocation Length (MSB)
            spt.Cdb[11] = 0x00; // Allocation Length (LSB)
            spt.Cdb[12] = 0x00; // Reserved
            spt.Cdb[13] = 0x00; // Reserved


            spt.Cdb[14] = 0x00; // Cache bit. 1 if no medium is mounted, 0 do not use cache, require live medium access
            spt.Cdb[15] = 0x00; // Should be optional
            spt.Length = (ushort)Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_DIRECT));

            //Is this Correct?
            spt.DataTransferLength = (uint)buffer.Length;
            spt.DataBuffer = dataHandle.AddrOfPinnedObject();

            ptrIn = Marshal.AllocHGlobal(Marshal.SizeOf(spt));
            Marshal.StructureToPtr(spt, ptrIn, true);

            bool result = DeviceIoControl(
                parent._tapeHandle,
                IOCTL_SCSI_PASS_THROUGH_DIRECT,
                ptrIn,
                (uint)Marshal.SizeOf(spt),
                ptrIn,
                (uint)Marshal.SizeOf(spt),
                out var iBytesReturned,
                IntPtr.Zero);

            if (!result)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SCSI READ ATTRIBUTE failed");

            SCSI_PASS_THROUGH_DIRECT retorno = (SCSI_PASS_THROUGH_DIRECT)Marshal.PtrToStructure(ptrIn, typeof(SCSI_PASS_THROUGH_DIRECT));

            dataHandle.Free();
            ParseAttributes(buffer);
        }
                
        private void ParseAttributes(byte[] buffer)
        {
            // If the system architecture is little-endian (that is, little end first),
            // reverse the byte array.
            byte[] tempbuffer = new byte[4];    // Table 72 — READ ATTRIBUTE: AVAILABLE DATA (n-3)
            Array.Copy(buffer, 0, tempbuffer, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tempbuffer);
            int avaiableData = BitConverter.ToInt32(tempbuffer, 0);  // representes the number of bytes of Attributes available in the buffer
            Console.WriteLine($"Numer of Bytes of Attributes returned: {avaiableData}");

            byte[] attributeBuffer = new byte[avaiableData];

            Array.Copy(buffer, 4, attributeBuffer, 0, avaiableData);

            
            Attributes.Clear();
            int offset = 0;
            int attributeHeader = 5; // Each attribute starts with 5 bytes: 2 for ID, 1 for type, 2 for length

            while (offset < attributeBuffer.Length)
            {
                ushort attrId = (ushort)((attributeBuffer[offset] << 8) | attributeBuffer[offset + 1]);
                ushort type = (ushort)((attributeBuffer[offset + 2]));
                ushort length = (ushort)((attributeBuffer[offset + 3] << 8) | attributeBuffer[offset + 4]);

                if (offset + attributeHeader + length > attributeBuffer.Length)
                    break;

                byte[] value = new byte[length];
                Array.Copy(attributeBuffer, offset + attributeHeader, value, 0, length);

                var KnownAttr = LTOCmKnownAttributes.GetAttributeByAddress(attrId);
                
                if (KnownAttr == null)
                {
                    KnownAttr = new LTOCmAttribute
                    {
                        Name = $"Unknown (0x{attrId:X4})",
                        Writable = false,
                        Format = LTOCmAttributeFormat.Unknown,
                        Length = -1
                    };
                };                


                KnownAttr.Address = attrId;
                KnownAttr.Value = new byte[length];
                Array.Copy(attributeBuffer, offset + attributeHeader, KnownAttr.Value, 0, length);

                if ((KnownAttr.Length != length) && (KnownAttr.Format != LTOCmAttributeFormat.Unknown))
                {
                    throw new ArgumentException($"Expected length is diferent from Length saved on the Dictionary. {attrId}: {KnownAttr.Length} - {length}");
                }

                Attributes.Add(KnownAttr);

                offset += length + attributeHeader;
            }
            Attributes.Sort((x, y) => x.Address.CompareTo(y.Address));            
        }


        public void WriteCartridgeAttribute(LTOCmAttribute aAttibute)
        {
            var KnownAttr = LTOCmKnownAttributes.GetAttributeByAddress(aAttibute.Address);
            if (KnownAttr == null)
            {
                throw new ArgumentException($"Unknown Attribute!");
            };

            if (!KnownAttr.Writable)
            {
                throw new ArgumentException($"Attribute {KnownAttr.Name} is not writable!");
            };

            if (aAttibute.Value == null)
            {
                throw new ArgumentException($"Attribute Value can't be null!");
            };


            IntPtr ptrIn = IntPtr.Zero;

            // We should construct the buffer with the attribute data
            byte[] buffer = new byte[MaxBufferSize];
                        
            // definir o tamanho total de bytes da mensagem
            buffer[0] = 0x00; // Parameter Data Leght MSB
            buffer[1] = 0x00; // Parameter Data Leght
            buffer[2] = 0x00; // Parameter Data Leght
            buffer[3] = 0x00; // Parameter Data Leght LSB

                        
            buffer[4] = (byte)(aAttibute.Address >> 8); // Attribute Identifier LSB
            buffer[5] = (byte)(aAttibute.Address & 0xFF); // Attribute Identifier LSB

            buffer[6] = 0x00; // Attribute READ ONLY (0x8 for Readonly, 0x0 read/write)

            switch (aAttibute.Format)
            {
                case LTOCmAttributeFormat.Binary:
                    buffer[7] = 0x00; // Attribute Type (0x00 for binary, 0x01 for ASCII, 0x10 for ASCII)
                    break;
                case LTOCmAttributeFormat.ASCII:
                    buffer[7] = 0x01; // Attribute Type (0x00 for binary, 0x01 for ASCII, 0x10 for ASCII)
                    break;
                case LTOCmAttributeFormat.Text:
                    buffer[7] = 0x10; // Attribute Type (0x00 for binary, 0x01 for ASCII, 0x10 for ASCII)
                    break;
                default:
                    throw new ArgumentException("Unknown attribute format");
            }            
            
            buffer[8] = (byte)(aAttibute.Length >> 8); // Attribute Length MSB
            buffer[9] = (byte)(aAttibute.Length & 0xFF); // Attribute Length LSB

            Array.Copy(aAttibute.Value,0,buffer,10, aAttibute.Length);


            GCHandle dataHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            SCSI_PASS_THROUGH_DIRECT spt = new SCSI_PASS_THROUGH_DIRECT();

            spt.DataIn = 0; // Data transfer direction: OUT
            spt.TimeOutValue = 60;
            spt.CdbLength = 16; // CDB length for READ ATTRIBUTE command
            spt.Cdb = new byte[spt.CdbLength];
            spt.Cdb[0] = 0x8D; // WRITE ATTRIBUTE command code
            spt.Cdb[1] = 0x01; // Write-through cache off
            spt.Cdb[2] = 0x00; // Obsolete
            spt.Cdb[3] = 0x00; // Obsolete
            spt.Cdb[4] = 0x00; // Obsolete
            spt.Cdb[5] = 0x00; // Logical Volume number
            spt.Cdb[6] = 0x00; // Reserved
            spt.Cdb[7] = 0x00; // Partition Number

            spt.Cdb[8] = 0x00; // Reserved
            spt.Cdb[9] = 0x00; // Reserved  

            //// Isnt Allocation Length size, 4 Bytes?
            spt.Cdb[10] = 0x00; // PARAMETER LIST LENGTH (MSB)
            spt.Cdb[11] = 0x00; // PARAMETER LIST LENGTH
            spt.Cdb[12] = 0x00; // PARAMETER LIST LENGTH
            spt.Cdb[13] = 0x01; // PARAMETER LIST LENGTH (LSB)

            spt.Cdb[14] = 0x00; // Reserved
            spt.Cdb[15] = 0x00; // Control Byte should be zero
            spt.Length = (ushort)Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_DIRECT));

            //Is this Correct?
            spt.DataTransferLength = (uint)buffer.Length;
            spt.DataBuffer = dataHandle.AddrOfPinnedObject();

            ptrIn = Marshal.AllocHGlobal(Marshal.SizeOf(spt));
            Marshal.StructureToPtr(spt, ptrIn, true);

            bool result = DeviceIoControl(
                parent._tapeHandle,
                IOCTL_SCSI_PASS_THROUGH_DIRECT,
                ptrIn,
                (uint)Marshal.SizeOf(spt),
                ptrIn,
                (uint)Marshal.SizeOf(spt),
                out var iBytesReturned,
                IntPtr.Zero);

            if (!result)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SCSI READ ATTRIBUTE failed");

            dataHandle.Free();
        }
    }
}
#endregion