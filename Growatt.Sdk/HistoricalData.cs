namespace Growatt.OSS
{
    public class HistoricalData
    {
        #region Properties

        public List<DeviceNoahHistoricalData> Datas { get; set; }
        public string EndDate { get; set; }
        public bool HaveNext { get; set; }
        public int Start { get; set; }

        #endregion Properties
    }
}
