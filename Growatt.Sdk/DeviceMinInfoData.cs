using Newtonsoft.Json;

namespace EnergyAutomate.Growatt
{
    public class DeviceMinInfoData
    {
        public int id { get; set; }
        public string serialNum { get; set; }
        public string portName { get; set; }
        public string dataLogSn { get; set; }
        public int groupId { get; set; }
        public string alias { get; set; }
        public string location { get; set; }
        public int addr { get; set; }
        public string fwVersion { get; set; }
        public long model { get; set; }
        public string innerVersion { get; set; }
        public bool lost { get; set; }
        public int status { get; set; }
        public string tcpServerIp { get; set; }
        public long lastUpdateTime { get; set; }
        public string sysTime { get; set; }
        public int deviceType { get; set; }
        public string communicationVersion { get; set; }
        public int pmax { get; set; }
        public int comAddress { get; set; }
        public int dtc { get; set; }
        public int countrySelected { get; set; }
        public int startTime { get; set; }
        public int restartTime { get; set; }
        public int wselectBaudrate { get; set; }
        public int trakerModel { get; set; }
        public int priorityChoose { get; set; }
        public int batteryType { get; set; }
        public int batSeriesNum { get; set; }
        public int batParallelNum { get; set; }
        public int bctMode { get; set; }
        public int bctAdjust { get; set; }
        public int bagingTestStep { get; set; }
        public double vnormal { get; set; }
        public double mppt { get; set; }
        public double batTempLowerLimitD { get; set; }
        public double batTempUpperLimitD { get; set; }
        public double batTempLowerLimitC { get; set; }
        public double batTempUpperLimitC { get; set; }
        public double vbatWarning { get; set; }
        public double vbatWarnClr { get; set; }
        public int modbusVersion { get; set; }
        public string manufacturer { get; set; }
        public string bdc1Sn { get; set; }
        public string bdc1Model { get; set; }
        public string bdc1Version { get; set; }
        public double vbatStopForDischarge { get; set; }
        public double vbatStopForCharge { get; set; }
        public double vbatStartForDischarge { get; set; }
        public string userName { get; set; }
        public string modelText { get; set; }
        public int plantId { get; set; }
        public string plantname { get; set; }
        public double timezone { get; set; }
        public double pCharge { get; set; }
        public double pDischarge { get; set; }
        public bool updating { get; set; }
        public double power { get; set; }
        public double eToday { get; set; }
        public double eTotal { get; set; }
        
        public double energyMonth { get; set; }
        public int strNum { get; set; }
        public string liBatteryManufacturers { get; set; }
        public string liBatteryFwVersion { get; set; }
        public string bmsSoftwareVersion { get; set; }
        public int bmsCommunicationType { get; set; }
        public string monitorVersion { get; set; }
        public int bdcMode { get; set; }
        public int bdcAuthversion { get; set; }
        public string hwVersion { get; set; }
        public int vppOpen { get; set; }
        public int level { get; set; }
        public string lastUpdateTimeText { get; set; }
        public string treeName { get; set; }
        public string treeID { get; set; }
        public string parentID { get; set; }
        public string imgPath { get; set; }
        public string statusText { get; set; }
        public string powerMaxText { get; set; }
        public string energyMonthText { get; set; }

        [JsonIgnore]
        public DateTimeOffset TS { get; set; }
    }
}

