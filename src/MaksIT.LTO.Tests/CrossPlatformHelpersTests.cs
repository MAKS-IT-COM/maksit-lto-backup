using System.Text;
using MaksIT.LTO.Core.MassStorage;
using MaksIT.LTO.Core.Networking;


namespace MaksIT.LTO.Tests;

public class CrossPlatformHelpersTests {
  [Fact]
  public void MamAttributeCodec_RoundTripsWriteParameterListHeader() {
    LTOCmAttribute lTOCmAttribute = LTOCmKnownAttributes.GetAttributeByAddress(2054) ?? throw new InvalidOperationException("Missing barcode attribute definition.");
    lTOCmAttribute.Value = Encoding.ASCII.GetBytes("TESTBAR1");
    lTOCmAttribute.Length = lTOCmAttribute.Value.Length;
    byte[] array = MamAttributeCodec.BuildWriteAttributeParameterList(lTOCmAttribute);
    Assert.True(array.Length >= 10 + lTOCmAttribute.Value.Length);
    Assert.Equal(8, array[4]);
    Assert.Equal(6, array[5]);
  }

  [Fact]
  public void ParseSmbPath_AcceptsUncAndSmbUrl() {
    (string, string, string) tuple = RemotePathAccess.ParseSmbPath("\\\\fileserver\\backups\\job1");
    Assert.Equal("fileserver", tuple.Item1);
    Assert.Equal("backups", tuple.Item2);
    Assert.Equal("job1", tuple.Item3);
    (string, string, string) tuple2 = RemotePathAccess.ParseSmbPath("smb://fileserver/backups/job1/sub");
    Assert.Equal("fileserver", tuple2.Item1);
    Assert.Equal("backups", tuple2.Item2);
    Assert.Equal("job1\\sub", tuple2.Item3);
  }
}
