using EnergyAutomate.Emulator.Models;
using System;
using System.Collections.Generic;

namespace EnergyAutomate.Emulator
{

    public class GrowatttRegister
    {
        public GrowattRegisterPosition Position { get; set; } = new();
        public GrowattData Data { get; set; } = new();
    }



}