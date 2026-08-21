namespace MaksIT.LTO.Core.MassStorage;


/// <summary>Cross-platform tape position returned by <see cref="ITapeDrive.GetPosition"/>.</summary>
public sealed class TapePosition {
  public uint? MethodType { get; set; }
  public uint? Partition { get; set; }
  public uint? OffsetLow { get; set; }
  public uint? OffsetHigh { get; set; }
  public int? Error { get; set; }
}
