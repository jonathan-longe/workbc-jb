﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkBC.Data;
using WorkBC.Data.Model.JobBoard;
using WorkBC.Shared.Settings;

namespace WorkBC.Shared.Services
{
    public class GeocodingService : IGeocodingService
    {
        private readonly JobBoardContext _dbContext;
        private readonly ILogger<IGeocodingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public GeocodingService(JobBoardContext dbContext, IConfiguration configuration,
            ILogger<IGeocodingService> logger=null, HttpClient client=null)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;

            var proxySettings = new ProxySettings();
            _configuration.GetSection("ProxySettings").Bind(proxySettings);

            //create persistent HttpClient if not provided as a dependency
            _httpClient = client ?? CreateHttpClient(proxySettings);
        }

        public async Task<GeocodedLocationCache> GetLocation(string location)
        {
            GeocodedLocationCache cachedLocation =
                await _dbContext.GeocodedLocationCache.FirstOrDefaultAsync(g => g.Name == location);

            if (cachedLocation != null)
            {
                return cachedLocation;
            }

            return await CreateLocation(location);
        }

        private async Task<GeocodedLocationCache> CreateLocation(string location)
        {
            //Get the long and lat of the current location
            (string lat, string lon, string city, string frenchCity, string province) geo =
                await GetGoogleMapsLocation(location);

            if (geo.lat != null && geo.lon != null)
            {
                //Save to database
                var glc = new GeocodedLocationCache
                {
                    DateGeocoded = DateTime.Now,
                    Name = location,
                    Latitude = geo.lat,
                    Longitude = geo.lon,
                    City = geo.city,
                    FrenchCity = geo.frenchCity,
                    Province = geo.province
                };

                _dbContext.GeocodedLocationCache.Add(glc);
                await _dbContext.SaveChangesAsync();

                //return location
                return glc;
            }

            return null;
        }

        private async Task<(string lat, string lon, string city, string frenchCity, string province)>
            GetGoogleMapsLocation(string address)
        {
            XDocument xdoc = null;

            try
            {
                //read API key
                string apiKey = _configuration["AppSettings:GoogleMapsIPApi"];

                var requestUri =
                    $"https://maps.googleapis.com/maps/api/geocode/xml?address={Uri.EscapeDataString(address)}&key={apiKey}";

                //Read into XDocument
                xdoc = await GetWebResponse(requestUri);

                //Get the result node
                XElement result = xdoc.Element("GeocodeResponse")?.Element("result");

                if (result == null)
                {
                    return (null, null, null, null, null);
                }

                //Parse the city
                string city = GetCity(result);

                // Get the province
                string province = result.Elements("address_component")
                    ?.Where(x => x.Element("type")?.Value == "administrative_area_level_1")
                    ?.SingleOrDefault()
                    ?.Element("short_name")
                    ?.Value;

                //Get the location node
                XElement locationElement = result?.Element("geometry")?.Element("location");

                //Read the long and lat from the XML
                XElement lat = locationElement?.Element("lat");
                XElement lng = locationElement?.Element("lng");

                string frenchCity = null;

                // only look up the French city if the address contains a number
                // (because we only really use it for postal codes)
                if (address.Any(char.IsDigit)) 
                {
                    //Get the french version
                    XDocument frenchXDoc = await GetWebResponse(requestUri, true);

                    //Get the french result node
                    XElement frenchResult = frenchXDoc.Element("GeocodeResponse")?.Element("result");

                    //Parse the french city
                    frenchCity = GetCity(frenchResult);
                }

                //Return long and lat
                return (lat?.Value, lng?.Value, city, frenchCity, province);
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _logger.LogError("GetGoogleMapsLocation(string address, IConfiguration configuration) : " +
                                     ex.Message);

                    //Most likely a problem with the Google API
                    //More details will be in the response from the API
                    if (xdoc != null)
                    {
                        _logger.LogError("Google API Response : " + xdoc);
                    }
                }
            }

            //Error
            return (null, null, null, null, null);
        }

        private static string GetCity(XElement result)
        {
            string city = result?.Elements("address_component")
                ?.Where(x => x.Element("type")?.Value == "locality")
                ?.SingleOrDefault()
                ?.Element("short_name")
                ?.Value;

            if (city == null)
            {
                city = result?.Elements("address_component")
                    ?.Where(x => x.Element("type")?.Value == "neighborhood")
                    ?.SingleOrDefault()
                    ?.Element("short_name")
                    ?.Value;
            }

            return city;
        }

        private static HttpClient CreateHttpClient(ProxySettings proxySettings)
        {
            var handler = new HttpClientHandler();

            if (proxySettings.UseProxy)
            {
                handler.Proxy = new WebProxy(proxySettings.ProxyHost, proxySettings.ProxyPort)
                {
                    BypassProxyOnLocal = true
                };
            }

            if (proxySettings.IgnoreSslErrors)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) => true;
            }

            return new HttpClient(handler);
        }

        /// <summary>
        ///     Get XML data from URL response
        /// </summary>
        private async Task<XDocument> GetWebResponse(string url, bool isFrench = false)
        {

            try
            {
                //response from server
                string responseFromServer;

                if (isFrench)
                {
                    url += "&language=fr";
                }

                //Read the web response from URL
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                //Save response
                responseFromServer = await response.Content.ReadAsStringAsync();

                //load web request to xml
                return XDocument.Parse(responseFromServer);
            }
            catch
            {
                return null;
            }
        }
    }
}