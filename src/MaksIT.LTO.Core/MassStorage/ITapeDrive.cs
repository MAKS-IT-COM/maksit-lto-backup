namespace MaksIT.LTO.Core.MassStorage;

public interface ITapeDrive : IDisposable {
  string DevicePath { get; }

  int WriteData(byte[] data);

  int ReadData(byte[] buffer, int offset, int length);

  byte[] ReadData(uint length);

  void WaitForTapeReady();

  int Erase(uint type);

  void Prepare(uint operation);

  int WriteMarks(uint type, uint count);

  TapePosition GetPosition(uint type, uint partition = 0u, uint offsetLow = 0u, uint offsetHigh = 0u);

  void SetPosition(uint method, uint partition = 0u, long offset = 0L);

  void SetMediaParams(uint blockSize);

  int GetStatus();

  IReadOnlyList<LTOCmAttribute> ReadCartridgeAttributes();

  void WriteCartridgeAttribute(LTOCmAttribute attribute);
}
