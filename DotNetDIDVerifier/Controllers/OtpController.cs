using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DIDVerifier.Controllers
{
    [ApiController]
    public class OtpController : ControllerBase
    {

        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpController> _log;

        public OtpController(ILogger<OtpController> logger, IMemoryCache memoryCache)
        {
            _cache = memoryCache;
            _log = logger;
        }
        private string GenerateOTPCode()
        {
            // generate the OTP (unix epoch time in reverse)
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            string otpCode = secondsSinceEpoch.ToString();
            char[] charArray = otpCode.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }


        [HttpPost]
        [Route("/generate-otp")]
        public async Task<ActionResult> GenerateOTP()
        {
            //string ID = this.Request.Query["ID"];
            string requestBody = await new StreamReader(this.Request.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string ID = data?.ID;

            if (string.IsNullOrEmpty(ID))
            {
                var err = new { version = "1.0.0", status = (int)HttpStatusCode.BadRequest, userMessage = "Invalid call - Missing parameters" };
                _log.LogInformation("/generate-otp: ERROR: " + err.userMessage);
                return BadRequest(err);
            }
            string otpCode = GenerateOTPCode();

            string blobData = JsonConvert.SerializeObject(new { ID = ID, OTP = otpCode, exp = DateTime.UtcNow.AddMinutes(5).ToString() });

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5));
            _cache.Set( otpCode, blobData, cacheEntryOptions);

            string jsonToReturn = JsonConvert.SerializeObject(new { ID = ID, OTP = otpCode });
            _log.LogInformation("/generate-otp: OK: " + jsonToReturn );

            return new ContentResult { ContentType = "application/json", Content = jsonToReturn };
        }

        [HttpPost]
        [Route("/validate-otp")]
        public async Task<ActionResult> ValidateOTP()
        {
            //string otpCode = this.Request.Query["OTP"];
            string requestBody = await new StreamReader(this.Request.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string otpCode = data?.OTP;

            if (string.IsNullOrEmpty(otpCode))
            {
                var err = new { version = "1.0.0", status = (int)HttpStatusCode.BadRequest, userMessage = "Invalid call - Missing parameters" };
                _log.LogInformation("/validate-otp: ERROR: " + err.userMessage);
                return BadRequest(err);
            }
            string blobData = "";
            if (!_cache.TryGetValue(otpCode, out blobData))
            {
                var err = new { version = "1.0.0", status = (int)HttpStatusCode.BadRequest, userMessage = "Unknown OTP" };
                _log.LogInformation("/validate-otp: ERROR: " + err.userMessage);
                return BadRequest(err);
            }
            JObject json = (JObject)JsonConvert.DeserializeObject(blobData);
            bool ok = false;
            string jsonToReturn = "";
            // The OTP must not be expired
            if (json["exp"] != null)
            {
                DateTime exp = DateTime.Parse(json["exp"].ToString());
                if (DateTime.UtcNow < exp)
                {
                    string dynData = CallDynamicsAPI(json["ID"].ToString() );
                    if (!string.IsNullOrWhiteSpace(dynData))
                    {
                        JObject objDynamics = JObject.Parse(dynData);
                        json.Merge(objDynamics, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
                        jsonToReturn = JsonConvert.SerializeObject(json);
                        ok = true;
                    }
                }
                _cache.Remove(otpCode);
            }

            if ( !ok )
            {
                var err = new { version = "1.0.0", status = (int)HttpStatusCode.BadRequest, userMessage = "Invalid OTP" };
                _log.LogInformation("/validate-otp: ERROR: " + err.userMessage);
                return BadRequest(err);
            }

            _log.LogInformation("/validate-otp: OK: " + jsonToReturn);
            return new ContentResult { ContentType = "application/json", Content = jsonToReturn };
        }

        private string CallDynamicsAPI(string ID )
        {
            // call Dynamics to get data - here we hard code if for now
            /*
            JObject objDynamics = JObject.Parse(@"{
                                                    'FirstName':  'John',
                                                    'DisplayName':  'John Doe',
                                                    'LastName':  'Doe',
                                                    'DateOfBirth':  '1998-12-01',
                                                    'Classification':  '4',
                                                    'RationCardID':  '9988776655',
                                                    'Gender':  'M'
                                                }");
            */

            // change the below to point to your CRM system to get user attributes
            string contents = "";
            string url = "https://cljungdemob2c-mockup-api.azurewebsites.net/api/UNHCRDynamicsMockup?code=s51pmdGk57CK5vobnxtG5lHV8eXHWPDF7duIMmsCklc2L/I2Kff7Ng==";
            HttpClient client = new HttpClient();
            string jsonBody = "{'ID': \"" + ID + "\"}";
            _log.LogInformation("CallDynamicsAPI Body=" + jsonBody);
            HttpContent body = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage res = client.PostAsync(url, body).Result;
            client.Dispose();
            _log.LogInformation("CallDynamicsAPI HttpStatusCode=" + res.StatusCode.ToString());
            // return either goo message that REST API expects or 409 conflict    
            if (res.StatusCode == HttpStatusCode.OK)
            {
                contents = res.Content.ReadAsStringAsync().Result;
            }
            return contents;
        }
    } // cls
} // ns
