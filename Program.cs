using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;

namespace SoundPhysicianDocker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .Build();

            ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

            logger.LogInformation("Application started.");

            // Read client credentials from environment variables, fallback to appsettings.json
            string clientId = Environment.GetEnvironmentVariable("CLIENT_ID")
                ?? config["ClientId"]
                ?? throw new Exception("ClientId not set in environment variables or appsettings.json");

            string clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET")
                ?? config["ClientSecret"]
                ?? throw new Exception("ClientSecret not set in environment variables or appsettings.json");

            string token = await GetAuthToken(clientId, clientSecret, logger);
            if (!string.IsNullOrEmpty(token))
            {
                logger.LogInformation("Token retrieved successfully.");

                string jsonResponse = await GetJobPostings(token, logger);
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    logger.LogInformation("Job postings retrieved.");

                    string xmlPath = config["XmlOutputPath"] ?? "output/jobs.xml";
                    ConvertJsonToXmlAndSave(jsonResponse, xmlPath, logger);

                    logger.LogInformation($"XML saved to {xmlPath}");
                }
            }
            else
            {
                logger.LogError("Failed to retrieve token.");
            }

            logger.LogInformation("Application ended.");
        }

        static async Task<string> GetAuthToken(string clientId, string clientSecret, ILogger logger)
        {
            try
            {
                using HttpClient client = new();

                var request = new HttpRequestMessage(HttpMethod.Post, "https://soundphysicians--uat.sandbox.my.salesforce.com/services/oauth2/token");

                var content = new MultipartFormDataContent
                {
                    { new StringContent("client_credentials"), "grant_type" },
                    { new StringContent(clientId), "client_id" },
                    { new StringContent(clientSecret), "client_secret" }
                };

                request.Content = content;

                HttpResponseMessage response = await client.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(responseString);
                    return json["access_token"]?.ToString() ?? string.Empty;
                }
                else
                {
                    logger.LogError("Failed to get token: {Response}", responseString);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while getting auth token");
                return string.Empty;
            }
        }

        static async Task<string> GetJobPostings(string token, ILogger logger)
        {
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await client.GetAsync("https://soundphysicians--uat.sandbox.my.salesforce.com/services/apexrest/SoundCareers/all");
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return responseString;
                }
                else
                {
                    logger.LogError("Failed to get job postings: {Response}", responseString);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while getting job postings");
                return string.Empty;
            }
        }

        static void ConvertJsonToXmlAndSave(string json, string filePath, ILogger logger)
        {
            try
            {
                JArray jsonArray = JArray.Parse(json);

                JObject wrappedJson = new();
                wrappedJson["Root"] = jsonArray;

                XmlDocument doc = JsonConvert.DeserializeXmlNode(wrappedJson.ToString(), "Roots");

                // Ensure directory exists
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    logger.LogInformation("Created directory {Dir}", dir);
                }

                doc.Save(filePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while converting JSON to XML or saving file");
            }
        }
    }
}
