using Microsoft.Extensions.Logging;


namespace MaksIT.LTO.Core.MassStorage;

public sealed class WindowsTapeDrive(ILogger<TapeDeviceHandler> logger, string tapePath) : ITapeDrive, IDisposable {
  private readonly TapeDeviceHandler _handler = new TapeDeviceHandler(logger, tapePath);

  public string DevicePath => tapePath;

  public int WriteData(byte[] data) => _handler.WriteData(data);

  public int ReadData(byte[] buffer, int offset, int length) => _handler.ReadData(buffer, offset, length);

  public byte[] ReadData(uint length) => _handler.ReadData(length);

  public void WaitForTapeReady() => _handler.WaitForTapeReady();

  public int Erase(uint type) => _handler.Erase(type);

  public void Prepare(uint operation) => _handler.Prepare(operation);

  public int WriteMarks(uint type, uint count) => _handler.WriteMarks(type, count);

  public TapePosition GetPosition(uint type, uint partition = 0u, uint offsetLow = 0u, uint offsetHigh = 0u) => _handler.GetPosition(type, partition, offsetLow, offsetHigh);

  public void SetPosition(uint method, uint partition = 0u, long offset = 0L) => _handler.SetPosition(method, partition, offset);

  public void SetMediaParams(uint blockSize) => _handler.SetMediaParams(blockSize);

  public int GetStatus() => _handler.GetStatus();

  public IReadOnlyList<LTOCmAttribute> ReadCartridgeAttributes() {
    _handler.ltoCartridgeMemory?.ReadCartridgeAttributes();
    return _handler.ltoCartridgeMemory?.Attributes?.ToList() ?? new List<LTOCmAttribute>();
  }

  public void WriteCartridgeAttribute(LTOCmAttribute attribute) => _handler.ltoCartridgeMemory?.WriteCartridgeAttribute(attribute);

  public void Dispose() => _handler.Dispose();
}
