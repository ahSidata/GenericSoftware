namespace EnergyAutomate.Emulator.Models
{
    public class GrowattEnumOptions
    {
        public string EnumType { get; set; } = string.Empty;
        public Dictionary<string, string> Values { get; set; } = new();
    }
}