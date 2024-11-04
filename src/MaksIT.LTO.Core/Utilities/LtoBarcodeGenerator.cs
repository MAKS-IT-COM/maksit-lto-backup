using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaksIT.LTO.Core.Utilities;
public static class LtoBarcodeGenerator
{
    public static string GenerateLTOBarcode(string volumeIdentifier, int ltoGeneration)
    {
        if (volumeIdentifier.Length != 6)
        {
            throw new ArgumentException("Volume identifier must be exactly 6 characters.");
        }

        // Append LTO generation code as the 7th character (e.g., "L6" for LTO-6)
        string generationCode = $"L{ltoGeneration}";

        // Combine the volume identifier with the generation code
        string barcodeContent = volumeIdentifier + generationCode;

        // Return the full barcode content as a string
        return barcodeContent;
    }
}
