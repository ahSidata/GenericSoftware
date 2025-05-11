using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Cryptography;

namespace EnergyAutomate.Emulator;

public class ShellyPro3EMDevice
{
    // --- Befehl-Dispatch ---
    public string HandleCommand(string json, string macAddress)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int id = root.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var intId) ? intId : 0;
            string? method = root.TryGetProperty("method", out var m) ? m.GetString() : null;

            // Mapping aller Shelly-Kommandos wie im Java-Emulator
            object response = method switch
            {
                "EM.GetStatus" => GetEmStatus(id, macAddress),
                "EM1.GetStatus" => GetEm1Status(id, macAddress),
                _ => new { id, error = $"Unknown method: {method}" },
            };
            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // --- Shelly-Kommandos ---

    int count = 0;

    public ShellyEmResponse GetEmStatus(int id, string mac) {
        switch(count)
        {
            case 0:
                count++;
                return new ShellyEmResponse(id, mac, 90.0f);
            case 1:
                count++;
                return new ShellyEmResponse(id, mac, 90.0f);
            case 2:
                count++;
                return new ShellyEmResponse(id, mac, 90.0f);
            case 3:
                count++;
                return new ShellyEmResponse(id, mac, 100.0f);
            default:
                count = 0;
                return new ShellyEmResponse(id, mac, 100.0f);
        }
    }

    public ShellyEm1Response GetEm1Status(int id, string mac)
    {
        switch (count)
        {
            case 0:
                count++;
                return new ShellyEm1Response(id, mac, 90.0f);
            case 1:
                count++;
                return new ShellyEm1Response(id, mac, 90.0f);
            case 2:
                count++;
                return new ShellyEm1Response(id, mac, 90.0f);
            case 3:
                count++;
                return new ShellyEm1Response(id, mac, 100.0f);
            default:
                count = 0;
                return new ShellyEm1Response(id, mac, 100.0f);
        }
    }

    // Hilfsklassen für Phasen/Energie (kannst du nach Bedarf erweitern)
}

public class ShellyEmResponse
{
    public ShellyEmResponse(int id, string macAddress, float act_power)
    {
        Id = 1;
        Src = $"shellypro3em-{macAddress}";
        Dst = "unknown";
        var voltage = 220.0f;
        var current = MathF.Round(act_power / voltage, 3);

        Result = new ShellyEmResponse.ParamsData
        {
            Id = 0,
            A_Act_Power = MathF.Round(act_power, 1),
            B_Act_Power = MathF.Round(act_power, 1),
            C_Act_Power = MathF.Round(act_power, 1),
            Total_Act_Power = MathF.Round(act_power * 3, 1),


            //A_Current = current,
            //A_Voltage = voltage,

            //A_Aprt_Power = MathF.Round(act_power, 1),
            A_Pf = 1,
            //A_Freq = 50,

            //B_Current = current,
            //B_Voltage = voltage,

            //B_Aprt_Power = MathF.Round(act_power, 1),
            B_Pf = 1,
            //B_Freq = 50,

            //C_Current = current,
            //C_Voltage = voltage,

            //C_Aprt_Power = MathF.Round(act_power, 1),

            C_Pf = 1,
            //C_Freq = 50,

            //Total_Current = MathF.Round(current * 3, 1),

            //Total_Aprt_Power = MathF.Round(act_power * 3, 1)
        };        
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("dst")]
    public string? Dst { get; set; }

    [JsonPropertyName("result")]
    public ParamsData? Result { get; set; }

    public class ParamsData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        //[JsonPropertyName("a_current")]
        //[JsonConverter(typeof(FloatThreeDecimalConverter))]
        //public float A_Current { get; set; }

        //[JsonPropertyName("a_voltage")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float A_Voltage { get; set; }

        [JsonPropertyName("a_act_power")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float A_Act_Power { get; set; }

        //[JsonPropertyName("a_aprt_power")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float A_Aprt_Power { get; set; }

        [JsonPropertyName("a_pf")]
        public float A_Pf { get; set; }

        //[JsonPropertyName("a_freq")]
        //public float A_Freq { get; set; }


        //[JsonPropertyName("b_current")]
        //[JsonConverter(typeof(FloatThreeDecimalConverter))]
        //public float B_Current { get; set; }

        //[JsonPropertyName("b_voltage")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float B_Voltage { get; set; }

        [JsonPropertyName("b_act_power")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float B_Act_Power { get; set; }

        //[JsonPropertyName("b_aprt_power")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float B_Aprt_Power { get; set; }

        [JsonPropertyName("b_pf")]
        public float B_Pf { get; set; }

        //[JsonPropertyName("b_freq")]
        //public float B_Freq { get; set; }

        //[JsonPropertyName("c_current")]
        //[JsonConverter(typeof(FloatThreeDecimalConverter))]
        //public float C_Current { get; set; }

        //[JsonPropertyName("c_voltage")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float C_Voltage { get; set; }

        [JsonPropertyName("c_act_power")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float C_Act_Power { get; set; }

        //[JsonPropertyName("c_aprt_power")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float C_Aprt_Power { get; set; }

        [JsonPropertyName("c_pf")]
        public float C_Pf { get; set; }

        //[JsonPropertyName("c_freq")]
        //public float C_Freq { get; set; }

        //[JsonPropertyName("total_current")]
        //[JsonConverter(typeof(FloatThreeDecimalConverter))]
        //public float Total_Current { get; set; }

        [JsonPropertyName("total_act_power")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float Total_Act_Power { get; set; }

        //[JsonPropertyName("total_aprt_power")]
        //[JsonConverter(typeof(FloatOneDecimalConverter))]
        //public float Total_Aprt_Power { get; set; }

        //[JsonPropertyName("user_calibrated_phase")]
        //public List<String> User_Valibrated_Phase { get; set; } = [];

        //[JsonPropertyName("errors")]

        //public List<String> Errors { get; set; } = [];

    }

    public class FloatThreeDecimalConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetSingle();
        }

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            // Format mit einer Nachkommastelle, auch wenn es eine 0 ist
            writer.WriteRawValue(value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public class FloatOneDecimalConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetSingle();
        }

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            // Format mit einer Nachkommastelle, auch wenn es eine 0 ist
            writer.WriteRawValue(value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

}

public class ShellyEm1Response
{
    public ShellyEm1Response(int id, string macAddress, float act_power)
    {
        Id = 1;
        Src = $"shelly3em-{macAddress}";
        Dst = "";
        var voltage = 220.0f;
        var current = MathF.Round(act_power / voltage, 3);

        Result = new ShellyEm1Response.ParamsData
        {
            Id = 0,
            Current = MathF.Round(current, 3),
            Voltage = MathF.Round(voltage, 1),
            Act_Power = MathF.Round(act_power, 1),
            Aprt_Power = MathF.Round(act_power, 1),
            Pf = 1,
            Freq = 50
        };
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("dst")]
    public string? Dst { get; set; }

    [JsonPropertyName("result")]
    public ParamsData? Result { get; set; }

    public class ParamsData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("voltage")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float Voltage { get; set; }

        [JsonPropertyName("current")]
        [JsonConverter(typeof(FloatThreeDecimalConverter))]
        public float Current { get; set; }

        [JsonPropertyName("act_power")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float Act_Power { get; set; }

        [JsonPropertyName("aprt_power")]
        [JsonConverter(typeof(FloatOneDecimalConverter))]
        public float Aprt_Power { get; set; }

        [JsonPropertyName("pf")]
        public float Pf { get; set; }

        [JsonPropertyName("freq")]
        public float Freq { get; set; }

        //[JsonPropertyName("calibration")]
        //public string Calibration { get; set; } = "factory";

        //[JsonPropertyName("flags")]
        //public List<string> Flags { get; set; } = [ "count_disabled" ];

    }

    public class FloatThreeDecimalConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetSingle();
        }

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            // Format mit einer Nachkommastelle, auch wenn es eine 0 ist
            writer.WriteRawValue(value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public class FloatOneDecimalConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetSingle();
        }

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            // Format mit einer Nachkommastelle, auch wenn es eine 0 ist
            writer.WriteRawValue(value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

}