namespace EnergyAutomate.Emulator.Growatt.Models
{

    public class GrowatttRegister
    {
        public GrowattRegisterPosition Position { get; set; } = new();
        public GrowattData Data { get; set; } = new();
    }



}