using EnergyAutomate.Definitions;
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

        #region Private Methods

        private async Task<T> ExecuteWithExceptionHandlingAsync<T>(Func<Task<T>> func)
        {
            try
            {
                return await func();
            }
            catch (ApiException apiException)
            {
                throw apiException;
            }
            catch (Exception ex)
            {
                throw new ApiException("API error: Other", -10, ex);
            }
        }

        #endregion Private Methods

        #region Public Methods

        public async Task<string> GetDataAsync(string endpoint)
        {
            return await ExecuteWithExceptionHandlingAsync(async () =>
            {
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            });
        }

        public async Task<List<DeviceNoahInfo>?> GetDeviceInfoAsync(IDeviceQuery deviceQuery)
        {
            return await ExecuteWithExceptionHandlingAsync(async () =>
            {
                var endpoint = "/v4/new-api/queryDeviceInfo";
                var content = deviceQuery.ToFormUrlEncodedContent();

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DeviceNoahInfoResponse>(responseString);

                if (result != null && result.Code == 0)
                {
                    return result.Data.Noah;
                }
                else if (result != null)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }

                return default;
            });
        }

        public async Task<List<DeviceNoahLastData>?> GetDeviceLastDataAsync(IDeviceQuery deviceQuery)
        {
            return await ExecuteWithExceptionHandlingAsync(async () =>
            {
                var endpoint = "/v4/new-api/queryLastData";
                var content = deviceQuery.ToFormUrlEncodedContent();

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

                return default;
            });
        }

        public async Task<List<DeviceList>?> GetDeviceListAsync(int page = 1)
        {
            return await ExecuteWithExceptionHandlingAsync(async () =>
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

                return default;
            });
        }

        public async Task<List<DeviceNoahHistoricalData>?> GetDevicesHistoricalDataAsync(IDeviceQuery deviceQuery)
        {
            return await ExecuteWithExceptionHandlingAsync(async () =>
            {
                var endpoint = "/v4/new-api/queryDevicesHistoricalData";
                var content = deviceQuery.ToFormUrlEncodedContent();

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

                return default;
            });
        }

        public async Task SetPowerAsync(IDeviceQuery deviceQuery)
        {
            await ExecuteWithExceptionHandlingAsync(async () =>
            {
                var endpoint = "/v4/new-api/setPower";
                var content = deviceQuery.ToFormUrlEncodedContent();

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);

                if (result != null && result.Code != 0)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }

                return Task.CompletedTask;
            });
        }

        public async Task SetTimeSegmentAsync(IDeviceQuery deviceQuery)
        {
            await ExecuteWithExceptionHandlingAsync(async () =>
            {
                var endpoint = "/v4/new-api/setTimeSegment";
                var content = deviceQuery.ToFormUrlEncodedContent();

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);

                if (result != null && result.Code != 0)
                {
                    throw new ApiException($"API error: {result.Message}", result.Code);
                }

                return Task.CompletedTask;
            });
        }

        #endregion Public Methods
    }
}
