using MaksIT.LTO.Core.Utilities;


namespace MaksIT.LTO.Tests;

public class DescriptorFormatTests {
  [Fact]
  public void PaddingUtility_RoundTripsWhenPayloadEndsWith0x80() {
    byte[] array = new byte[4] { 1, 2, 128, 128 };
    byte[] array2 = PaddingUtility.AddPadding(array, 8);
    byte[] actual = PaddingUtility.RemovePadding(array2, 8);
    Assert.Equal(array, actual);
    Assert.Equal(0, array2.Length % 8);
  }

  [Fact]
  public void DescriptorPreamble_RoundTripsBlockCount() {
    byte[] array = DescriptorPreamble.Create(3u, 100, 512);
    Assert.True(DescriptorPreamble.TryParse(array, out uint descriptorBlockCount, out int ciphertextLength, out string error));
    Assert.Null(error);
    Assert.Equal(3u, descriptorBlockCount);
    Assert.Equal(100, ciphertextLength);
  }

  [Fact]
  public void FileHashUtility_ComputesStableSha256() {
    string text = Path.Combine(Path.GetTempPath(), "lto-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(text);
    string text2 = Path.Combine(text, "a.bin");
    File.WriteAllBytes(text2, "abc"u8.ToArray());
    Assert.True(FileHashUtility.TryCalculateSha256FromFileInChunks(text2, out string hash, out string error, 64));
    Assert.Null(error);
    Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    Assert.True(FileHashUtility.VerifySha256FromFileInChunks(text2, hash, 64));
  }

  [Fact]
  public void BotIndex_RoundTripsSetTable() {
    byte[] contentHash = Convert.FromHexString("11".PadRight(64, '0'));
    var index = new BotIndex.Index {
      SchemaVersion = 2,
      BlockSize = 512u,
      Sets =
        [
            new BotIndex.SetEntry
                {
                    PayloadBaseBlock = 1u,
                    DescriptorAbsoluteBlock = 10u,
                    DescriptorBlockCount = 2u,
                    ContentHash = contentHash,
                    BackupName = "JobA"
                }
        ]
    };
    byte[] array = BotIndex.Create(index, 512);
    Assert.True(BotIndex.TryParse(array, out BotIndex.Index index2, out string error));
    Assert.Null(error);
    Assert.NotNull(index2);
    Assert.Single(index2.Sets);
    Assert.Equal(10u, index2.Sets[0].DescriptorAbsoluteBlock);
    Assert.Equal("JobA", index2.Sets[0].BackupName);
  }
}
