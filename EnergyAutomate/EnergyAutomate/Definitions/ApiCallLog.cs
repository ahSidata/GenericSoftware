namespace EnergyAutomate.Definitions
{
    public class ApiCallLog
    {
        #region Properties

        public string MethodeName { get; set; } = string.Empty;

        public bool RaisedError { get; set; }

        public DateTime TimeStamp { get; set; }

        #endregion Properties
    }
}
