using Growatt.Sdk;
using Newtonsoft.Json;

namespace Growatt.OSS
{
    public class GrowattApiClient
    {
        #region Fields

        private readonly HttpClient _httpClient;

        #endregion Fields

        #region Public Constructors

        public GrowattApiClient(string baseAddress, string token)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
            _httpClient.DefaultRequestHeaders.Add("token", token);
        }

        #endregion Public Constructors

        #region Public Methods

        public async Task<string> GetDataAsync(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<List<DeviceNoahInfo>?> GetDeviceInfoAsync(string deviceType, string deviceSn)
        {
            try
            {
                var endpoint = "/v4/new-api/queryDeviceInfo";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("deviceType", deviceType),
                    new KeyValuePair<string, string>("deviceSn", deviceSn)
                });

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DeviceInfoResponse>(responseString);

                if (result != null && result?.Code == 0)
                {
                    return result.Data.Noah;
                }
                else if (result != null)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }
            }
            catch (Exception ex)
            {
                throw new ApiException($"API error: Other", -10, ex);
            }
            return default;
        }

        public async Task<List<DeviceNoahLastData>?> GetDeviceLastDataAsync(string deviceType, string deviceSn)
        {
            try
            {
                var endpoint = "/v4/new-api/queryLastData";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("deviceType", deviceType),
                    new KeyValuePair<string, string>("deviceSn", deviceSn)
                });

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DeviceNoahLastDataResponse>(responseString);

                if (result != null && result.Code == 0)
                {
                    return result.Data.Noah;
                }
                else if (result != null)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }
            }
            catch (Exception ex)
            {
                throw new ApiException($"API error: Other", -10, ex);
            }
            return default;
        }

        public async Task<List<Device>?> GetDeviceListAsync(int page = 1)
        {
            try
            {
                var endpoint = "/v4/new-api/queryDeviceList";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("page", page.ToString())
                });

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DeviceListResponse>(responseString);

                if (result != null && result.Code == 0)
                {
                    return result.Data.Devices;
                }
                else if (result != null)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }
            }
            catch (Exception ex)
            {
                throw new ApiException($"API error: Other", -10, ex);
            }

            return default;
        }

        public async Task<List<DeviceNoahHistoricalData>?> GetDevicesHistoricalDataAsync(string deviceSn, string deviceType, string date)
        {
            try
            {
                var endpoint = "/v4/new-api/queryDevicesHistoricalData";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("deviceSn", deviceSn),
                    new KeyValuePair<string, string>("deviceType", deviceType),
                    new KeyValuePair<string, string>("date", date)
                });

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<HistoricalDataResponse>(responseString);

                if (result != null && result.Code == 0)
                {
                    return result.Data.Datas;
                }
                else if (result != null)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }
            }
            catch (Exception ex)
            {
                throw new ApiException($"API error: Other", -10, ex);
            }

            return default;
        }

        public async Task SetPowerAsync(string deviceSn, string deviceType, int value)
        {
            try
            {
                var endpoint = "/v4/new-api/setPower";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("deviceSn", deviceSn),
                    new KeyValuePair<string, string>("deviceType", deviceType),
                    new KeyValuePair<string, string>("value", value.ToString())
                });

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);

                if (result != null && result.Code != 0)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }
            }
            catch (Exception ex)
            {
                throw new ApiException($"API error: Other", -10, ex);
            }
        }

        public async Task SetTimeSegmentAsync(DeviceNoahTimeSegmentQuery deviceNoahTimeSegment)
        {
            try
            {
                var endpoint = "/v4/new-api/setTimeSegment";
                var content = deviceNoahTimeSegment.ToFormUrlEncodedContent();

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);

                if (result != null && result.Code != 0)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }
            }
            catch (Exception ex)
            {
                throw new ApiException($"API error: Other", -10, ex);
            }
        }

        #endregion Public Methods
    }
}
