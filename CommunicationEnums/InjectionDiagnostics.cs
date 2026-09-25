using System;

namespace XboxWheelCompatibility.CommunicationInterface
{
    [Serializable]
    public class InjectionDiagnostics
    {
        public bool InjectorAvailable { get; set; }
        public string? InjectorErrorMessage { get; set; }
        public long InjectAttempts { get; set; }
        public long InjectSuccesses { get; set; }
        public string? LastInjectError { get; set; }
    }
}
