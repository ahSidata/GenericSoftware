namespace EnergyAutomate.Emulator.Growatt
{
    public enum GrowattModbusFunction : byte
    {
        READ_HOLDING_REGISTER = 3,
        READ_INPUT_REGISTER = 4,
        READ_SINGLE_REGISTER = 5,
        PRESET_SINGLE_REGISTER = 6,
        PRESET_MULTIPLE_REGISTER = 16
    }
}