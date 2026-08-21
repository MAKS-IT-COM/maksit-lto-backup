namespace MaksIT.LTO.Core.MassStorage;

public interface ITapeLibrary {
  string DevicePath { get; }

  IReadOnlyList<LibraryElementStatus> GetElementStatus();

  void InitializeElementStatus();

  void MoveMedium(int sourceSlot, int destinationSlot);
}
