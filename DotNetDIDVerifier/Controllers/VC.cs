using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DIDVerifier.Controllers
{
    [ApiController]
    public class VCController : ControllerBase
    {

        [HttpPost]
        [Route("/presentation-response")]
        public async Task<IActionResult> Postpresentation_responseAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var targetURI = new Uri("http://localhost:8082/presentation-response");

                    var requestMessage = new HttpRequestMessage();
                    requestMessage.Method = HttpMethod.Post;
                    using (var streamContent = new StreamContent(Request.Body))
                    {
                        requestMessage.Content = streamContent;
                        foreach (var header in Request.Headers)
                        {
                            requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                        }
                        requestMessage.RequestUri = targetURI;
                        requestMessage.Headers.Host = targetURI.Host; //not sure if this is needed

                        //call the node.js method. If validating is succesful you can do some magic here to return some of the data from the VC you might need in the response message.
                        //currently it's just copying the data
                        using (var responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted))
                        {
                            //if statuscode = 200 validation succeeded, do the magic you need to do after validating here and return an OK to the authenticator app to finish the communication
                            HttpContext.Response.StatusCode = (int)responseMessage.StatusCode;
                            foreach (var header in responseMessage.Headers)
                            {
                                HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                            }
                            
                            foreach (var header in responseMessage.Content.Headers)
                            {
                                HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                            }

                            HttpContext.Response.Headers.Remove("transfer-encoding");
                            await responseMessage.Content.CopyToAsync(HttpContext.Response.Body);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok();
        }

    }
}
