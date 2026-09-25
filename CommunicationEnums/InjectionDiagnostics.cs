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

        /// <summary>E.g. "ViGEm Xbox 360", "InputInjector", "ViGEm Xbox 360 + InputInjector".</summary>
        public string ActiveOutputs { get; set; } = "";
        public bool ViGEmActive { get; set; }
        public string? ViGEmError { get; set; }
        /// <summary>0 = Auto, 1 = InputInjector, 2 = ViGEm, 3 = Both, 4 = None.</summary>
        public int OutputMode { get; set; }
    }
}
