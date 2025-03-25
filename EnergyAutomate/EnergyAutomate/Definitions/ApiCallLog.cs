namespace EnergyAutomate.Definitions
{
    public class ApiCallLog
    {
        #region Properties

        public DeviceNoahLastDataQuery.QueryTypes MethodeName { get; set; }

        public bool RaisedError { get; set; }

        public DateTime TimeStamp { get; set; }

        #endregion Properties
    }
}
