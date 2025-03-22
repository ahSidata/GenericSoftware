namespace EnergyAutomate
{
    public class ApiCallLog
    {
        public string MethodeName { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; }
        public bool RaisedError { get; set; }
    }
}
